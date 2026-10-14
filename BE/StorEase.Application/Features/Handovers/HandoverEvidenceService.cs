using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Handovers;

public sealed record HandoverPhotoDto(int HandoverPhotoId, int HandoverId, string FileName, string ContentType, int SizeBytes, DateTime UploadedAt);

/// <summary>A completed handover with its evidence; <see cref="AccessCode"/> is never shown again after the handover response.</summary>
public sealed record HandoverDetailDto(
    int HandoverId, int ReservationId, int? ContractId, int StorageUnitId, string UnitCode, int StaffId, string StaffName,
    DateTime HandoverTime, string? LockSerial, string? CardNumber, string? ConditionNote, HandoverStatus Status,
    IReadOnlyList<HandoverPhotoDto> Photos);

public sealed record PhotoFile(Stream Content, string ContentType, string FileName);

public interface IHandoverEvidenceService
{
    Task<Result<HandoverDetailDto>> GetAsync(int handoverId, CancellationToken ct);
    Task<Result<HandoverPhotoDto>> AddPhotoAsync(int handoverId, Stream content, string fileName, string contentType, long length, CancellationToken ct);
    Task<Result<PhotoFile>> OpenPhotoAsync(int handoverId, int photoId, CancellationToken ct);
}

/// <summary>Photos of the unit's condition at handover (BE-29). Staff upload them; the customer may view their own.</summary>
public class HandoverEvidenceService(IUnitOfWork uow, ICurrentUser currentUser, IFileStorage files) : IHandoverEvidenceService
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp",
    };

    public async Task<Result<HandoverDetailDto>> GetAsync(int handoverId, CancellationToken ct)
    {
        var handover = await Visible().AsNoTracking()
            .Include(h => h.StorageUnit).Include(h => h.Staff)
            .FirstOrDefaultAsync(h => h.HandoverId == handoverId, ct);
        if (handover is null)
            return Error.NotFound("Handover not found.");

        var photos = await uow.Repository<HandoverPhoto>().Query().Where(p => p.HandoverId == handoverId)
            .OrderBy(p => p.HandoverPhotoId).Select(p => ToDto(p)).ToListAsync(ct);
        return new HandoverDetailDto(handover.HandoverId, handover.ReservationId, handover.ContractId, handover.StorageUnitId,
            handover.StorageUnit.UnitCode, handover.StaffId, handover.Staff.FullName, handover.HandoverTime, handover.LockSerial,
            handover.CardNumber, handover.ConditionNote, handover.Status, photos);
    }

    public async Task<Result<HandoverPhotoDto>> AddPhotoAsync(
        int handoverId, Stream content, string fileName, string contentType, long length, CancellationToken ct)
    {
        if (!Extensions.TryGetValue(contentType, out var extension))
            return Error.Validation("Only JPEG, PNG or WebP photos are accepted.");
        if (length is <= 0 or > MaxBytes)
            return Error.Validation("A photo must be between 1 byte and 5 MB.");
        if (!await uow.Repository<Handover>().Query().AnyAsync(h => h.HandoverId == handoverId, ct))
            return Error.NotFound("Handover not found.");

        var stored = await files.SaveAsync(content, extension, ct);
        var photo = new HandoverPhoto
        {
            HandoverId = handoverId,
            UploadedBy = currentUser.UserId,
            FileName = Path.GetFileName(fileName) is { Length: > 0 and <= 200 } name ? name : $"photo{extension}",
            ContentType = contentType.ToLowerInvariant(),
            StoredName = stored,
            SizeBytes = (int)length,
            UploadedAt = DateTime.UtcNow,
        };
        uow.Repository<HandoverPhoto>().Add(photo);
        await uow.SaveChangesAsync(ct);
        return ToDto(photo);
    }

    public async Task<Result<PhotoFile>> OpenPhotoAsync(int handoverId, int photoId, CancellationToken ct)
    {
        if (!await Visible().AnyAsync(h => h.HandoverId == handoverId, ct))
            return Error.NotFound("Handover not found.");
        var photo = await uow.Repository<HandoverPhoto>().Query()
            .FirstOrDefaultAsync(p => p.HandoverPhotoId == photoId && p.HandoverId == handoverId, ct);
        var stream = photo is null ? null : files.OpenRead(photo.StoredName);
        return stream is null ? Error.NotFound("Photo not found.") : new PhotoFile(stream, photo!.ContentType, photo.FileName);
    }

    // A customer sees only the handover of their own reservation; another customer's is a 404.
    private IQueryable<Handover> Visible()
    {
        var query = uow.Repository<Handover>().Query();
        if (!currentUser.IsCustomer)
            return query;
        var customerId = currentUser.UserId;
        return query.Where(h => h.Reservation.CustomerId == customerId);
    }

    private static HandoverPhotoDto ToDto(HandoverPhoto p) =>
        new(p.HandoverPhotoId, p.HandoverId, p.FileName, p.ContentType, p.SizeBytes, p.UploadedAt);
}
