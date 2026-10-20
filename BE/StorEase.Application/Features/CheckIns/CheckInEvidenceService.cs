using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.CheckIns;

public sealed record CheckInPhotoDto(int CheckInPhotoId, int CheckInId, string FileName, string ContentType, int SizeBytes, DateTime UploadedAt);

/// <summary>A completed check-in with its evidence; <see cref="AccessCode"/> is never shown again after the check-in response.</summary>
public sealed record CheckInDetailDto(
    int CheckInId, int ReservationId, int? ContractId, int StorageUnitId, string UnitCode, int StaffId, string StaffName,
    DateTime CheckInTime, string? LockSerial, string? CardNumber, string? ConditionNote, CheckInStatus Status,
    IReadOnlyList<CheckInPhotoDto> Photos);

public sealed record PhotoFile(Stream Content, string ContentType, string FileName);

public interface ICheckInEvidenceService
{
    Task<Result<CheckInDetailDto>> GetAsync(int checkInId, CancellationToken ct);
    Task<Result<PagedResult<CheckInDetailDto>>> ListAsync(CheckInStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<CheckInPhotoDto>> AddPhotoAsync(int checkInId, Stream content, string fileName, string contentType, long length, CancellationToken ct);
    Task<Result<PhotoFile>> OpenPhotoAsync(int checkInId, int photoId, CancellationToken ct);
}

/// <summary>Photos of the unit's condition at check-in (BE-29). Staff upload them; the customer may view their own.</summary>
public class CheckInEvidenceService(IUnitOfWork uow, ICurrentUser currentUser, IFileStorage files) : ICheckInEvidenceService
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp",
    };

    public async Task<Result<CheckInDetailDto>> GetAsync(int checkInId, CancellationToken ct)
    {
        var checkIn = await Visible().AsNoTracking()
            .Include(h => h.StorageUnit).Include(h => h.Staff)
            .FirstOrDefaultAsync(h => h.CheckInId == checkInId, ct);
        if (checkIn is null)
            return Error.NotFound("Check-in not found.");

        var photos = await uow.Repository<CheckInPhoto>().Query().Where(p => p.CheckInId == checkInId)
            .OrderBy(p => p.CheckInPhotoId).Select(p => ToDto(p)).ToListAsync(ct);
        return new CheckInDetailDto(checkIn.CheckInId, checkIn.ReservationId, checkIn.ContractId, checkIn.StorageUnitId,
            checkIn.StorageUnit.UnitCode, checkIn.StaffId, checkIn.Staff.FullName, checkIn.CheckInTime, checkIn.LockSerial,
            checkIn.CardNumber, checkIn.ConditionNote, checkIn.Status, photos);
    }

    public async Task<Result<PagedResult<CheckInDetailDto>>> ListAsync(CheckInStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible().AsNoTracking();
        if (status is not null)
            query = query.Where(c => c.Status == status);
        var photos = uow.Repository<CheckInPhoto>().Query();
        return await query.OrderByDescending(c => c.CheckInTime)
            .Select(c => new CheckInDetailDto(c.CheckInId, c.ReservationId, c.ContractId, c.StorageUnitId, c.StorageUnit.UnitCode,
                c.StaffId, c.Staff.FullName, c.CheckInTime, c.LockSerial, c.CardNumber, c.ConditionNote, c.Status,
                photos.Where(p => p.CheckInId == c.CheckInId).OrderBy(p => p.CheckInPhotoId)
                    .Select(p => new CheckInPhotoDto(p.CheckInPhotoId, p.CheckInId, p.FileName, p.ContentType, p.SizeBytes, p.UploadedAt))
                    .ToList()))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<CheckInPhotoDto>> AddPhotoAsync(
        int checkInId, Stream content, string fileName, string contentType, long length, CancellationToken ct)
    {
        if (!Extensions.TryGetValue(contentType, out var extension))
            return Error.Validation("Only JPEG, PNG or WebP photos are accepted.");
        if (length is <= 0 or > MaxBytes)
            return Error.Validation("A photo must be between 1 byte and 5 MB.");
        if (!await Visible().AnyAsync(h => h.CheckInId == checkInId, ct))
            return Error.NotFound("Check-in not found.");

        var stored = await files.SaveAsync(content, extension, ct);
        var photo = new CheckInPhoto
        {
            CheckInId = checkInId,
            UploadedBy = currentUser.UserId,
            FileName = Path.GetFileName(fileName) is { Length: > 0 and <= 200 } name ? name : $"photo{extension}",
            ContentType = contentType.ToLowerInvariant(),
            StoredName = stored,
            SizeBytes = (int)length,
            UploadedAt = DateTime.UtcNow,
        };
        uow.Repository<CheckInPhoto>().Add(photo);
        await uow.SaveChangesAsync(ct);
        return ToDto(photo);
    }

    public async Task<Result<PhotoFile>> OpenPhotoAsync(int checkInId, int photoId, CancellationToken ct)
    {
        if (!await Visible().AnyAsync(h => h.CheckInId == checkInId, ct))
            return Error.NotFound("Check-in not found.");
        var photo = await uow.Repository<CheckInPhoto>().Query()
            .FirstOrDefaultAsync(p => p.CheckInPhotoId == photoId && p.CheckInId == checkInId, ct);
        var stream = photo is null ? null : files.OpenRead(photo.StoredName);
        return stream is null ? Error.NotFound("Photo not found.") : new PhotoFile(stream, photo!.ContentType, photo.FileName);
    }

    // A customer sees only the check-in of their own reservation, staff those of their facilities; anything else is a 404.
    private IQueryable<CheckIn> Visible()
    {
        var query = uow.Repository<CheckIn>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(h => h.Reservation.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(h => facilityIds.Contains(h.Reservation.FacilityId));
    }

    private static CheckInPhotoDto ToDto(CheckInPhoto p) =>
        new(p.CheckInPhotoId, p.CheckInId, p.FileName, p.ContentType, p.SizeBytes, p.UploadedAt);
}
