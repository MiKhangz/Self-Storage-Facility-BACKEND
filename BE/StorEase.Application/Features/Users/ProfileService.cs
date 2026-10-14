using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Auth.Validators;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;

namespace StorEase.Application.Features.Users;

public sealed record ProfileDto(
    int UserId, string FullName, string Email, string PhoneNumber, string? CitizenId, DateOnly? DateOfBirth,
    string? Address, string Role, string Status, DateTime CreatedAt);

/// <summary>Email is the sign-in name and is not editable here.</summary>
public sealed record UpdateProfileRequest(string FullName, string PhoneNumber, string? CitizenId, DateOnly? DateOfBirth, string? Address);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record NotificationSettingsDto(bool EmailReminders, bool SmsReminders, bool MarketingEmails);

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20).Matches(@"^\+?[0-9 ]{8,20}$");
        RuleFor(x => x.CitizenId).MaximumLength(20).Matches("^[0-9]{9,12}$").When(x => !string.IsNullOrEmpty(x.CitizenId));
        RuleFor(x => x.DateOfBirth).LessThan(_ => VietnamTime.Today).When(x => x.DateOfBirth.HasValue);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NewPassword).StrongPassword().NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password must differ from the current one.");
    }
}

public interface IProfileService
{
    Task<Result<ProfileDto>> GetAsync(CancellationToken ct);
    Task<Result<ProfileDto>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<Result<bool>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<Result<NotificationSettingsDto>> GetNotificationSettingsAsync(CancellationToken ct);
    Task<Result<NotificationSettingsDto>> UpdateNotificationSettingsAsync(NotificationSettingsDto request, CancellationToken ct);
}

/// <summary>Everything here acts on the signed-in user only (the <c>sub</c> claim), never on an id from the request.</summary>
public class ProfileService(IUnitOfWork uow, ICurrentUser currentUser, IPasswordHasher hasher) : IProfileService
{
    public static readonly NotificationSettingsDto Defaults = new(EmailReminders: true, SmsReminders: false, MarketingEmails: false);

    public async Task<Result<ProfileDto>> GetAsync(CancellationToken ct)
    {
        var dto = await uow.Repository<User>().Query().Where(u => u.UserId == currentUser.UserId)
            .Select(u => new ProfileDto(u.UserId, u.FullName, u.Email, u.PhoneNumber, u.CitizenId, u.DateOfBirth,
                u.Address, u.Role.RoleName, u.Status.ToString(), u.CreatedAt))
            .FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Account not found.") : dto;
    }

    public async Task<Result<ProfileDto>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await uow.Repository<User>().FindAsync(currentUser.UserId, ct);
        if (user is null)
            return Error.NotFound("Account not found.");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.CitizenId = string.IsNullOrWhiteSpace(request.CitizenId) ? null : request.CitizenId.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        await uow.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<Result<bool>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await uow.Repository<User>().FindAsync(currentUser.UserId, ct);
        if (user is null)
            return Error.NotFound("Account not found.");
        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Error.Validation("The current password is incorrect.");

        user.PasswordHash = hasher.Hash(request.NewPassword);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Result<NotificationSettingsDto>> GetNotificationSettingsAsync(CancellationToken ct)
    {
        var row = await uow.Repository<NotificationPreference>().Query()
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct);
        return row is null ? Defaults : new NotificationSettingsDto(row.EmailReminders, row.SmsReminders, row.MarketingEmails);
    }

    public async Task<Result<NotificationSettingsDto>> UpdateNotificationSettingsAsync(NotificationSettingsDto request, CancellationToken ct)
    {
        var row = await uow.Repository<NotificationPreference>().Query()
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct);
        if (row is null)
        {
            row = new NotificationPreference { UserId = currentUser.UserId };
            uow.Repository<NotificationPreference>().Add(row);
        }
        row.EmailReminders = request.EmailReminders;
        row.SmsReminders = request.SmsReminders;
        row.MarketingEmails = request.MarketingEmails;
        row.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return new NotificationSettingsDto(row.EmailReminders, row.SmsReminders, row.MarketingEmails);
    }
}
