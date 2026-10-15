using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.ActivityLogs;
using StorEase.Application.Features.Auth.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct);
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result<bool>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct);
    Task<Result<bool>> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct);
    Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
}

public class AuthService(
    IUnitOfWork uow, IPasswordHasher hasher, IJwtService jwt, IEmailService email, AppOptions app,
    IActivityLogService activity) : IAuthService
{
    private const string VerifyPurpose = "verify-email";
    private const string ResetPurpose = "reset-password";

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct)
    {
        var user = await uow.Repository<User>().Query()
            .Where(u => u.Email == request.Email)
            .Select(u => new { u.UserId, u.FullName, u.Email, u.PasswordHash, u.Status, u.Role.RoleName })
            .FirstOrDefaultAsync(ct);

        // One message for unknown email and wrong password, so login cannot be used to probe for accounts.
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null)
                await activity.RecordAsync(new ActivityEntry(user.UserId, null, "LOGIN", "User", user.UserId.ToString(), ipAddress, false), ct);
            return Error.Unauthorized("Invalid email or password.");
        }

        if (user.Status == UserStatus.Pending)
            return Error.Forbidden("Please verify your email address before signing in.");
        if (user.Status != UserStatus.Active)
            return Error.Forbidden("This account is not active.");

        await activity.RecordAsync(new ActivityEntry(user.UserId, null, "LOGIN", "User", user.UserId.ToString(), ipAddress, true), ct);
        var token = jwt.CreateToken(user.UserId, user.FullName, user.Email, user.RoleName);
        return new LoginResponse(token, user.UserId, user.FullName, user.RoleName);
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var emailAddress = request.Email.Trim().ToLowerInvariant();
        if (await uow.Repository<User>().Query().AnyAsync(u => u.Email == emailAddress, ct))
            return Error.Conflict("An account with this email already exists.");

        var role = await uow.Repository<Role>().Query().FirstOrDefaultAsync(r => r.RoleName == Roles.StorageCustomer, ct);
        if (role is null)
            throw new InvalidOperationException($"Role {Roles.StorageCustomer} is missing; run the database seeder.");

        var user = new User
        {
            RoleId = role.RoleId,
            FullName = request.FullName.Trim(),
            Email = emailAddress,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Status = app.AutoVerifyEmail ? UserStatus.Active : UserStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        uow.Repository<User>().Add(user);
        await uow.SaveChangesAsync(ct);

        if (user.Status == UserStatus.Pending)
            await SendVerificationAsync(user, ct);
        return new RegisterResponse(user.UserId, user.Email, user.Status.ToString());
    }

    public async Task<Result<bool>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var claim = jwt.ReadPurposeToken(request.Token, VerifyPurpose);
        var user = claim is null ? null : await uow.Repository<User>().FindAsync(claim.Value.UserId, ct);
        if (user is null || claim!.Value.Stamp != EmailStamp(user))
            return Error.Validation("This verification link is invalid or has expired.");
        if (user.Status != UserStatus.Pending)
            return true; // opening the link twice is harmless

        user.Status = UserStatus.Active;
        await uow.SaveChangesAsync(ct);
        return true;
    }

    // Always succeeds so the response cannot reveal which emails have accounts.
    public async Task<Result<bool>> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct)
    {
        var user = await FindByEmailAsync(request.Email, ct);
        if (user is { Status: UserStatus.Pending })
            await SendVerificationAsync(user, ct);
        return true;
    }

    // Always succeeds so the response cannot reveal which emails have accounts.
    public async Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await FindByEmailAsync(request.Email, ct);
        if (user is { Status: UserStatus.Active or UserStatus.Pending })
        {
            var token = jwt.CreatePurposeToken(user.UserId, ResetPurpose, PasswordStamp(user), TimeSpan.FromMinutes(30));
            await email.SendAsync(user.Email, "Đặt lại mật khẩu StorEase",
                $"Xin chào {user.FullName},\n\nMở liên kết sau để đặt lại mật khẩu (hết hạn sau 30 phút):\n" +
                $"{app.FrontendBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}\n\n" +
                "Nếu bạn không yêu cầu, hãy bỏ qua email này.", ct);
        }
        return true;
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var claim = jwt.ReadPurposeToken(request.Token, ResetPurpose);
        var user = claim is null ? null : await uow.Repository<User>().FindAsync(claim.Value.UserId, ct);
        // The stamp is derived from the current hash, so a link stops working as soon as it has been used once.
        if (user is null || claim!.Value.Stamp != PasswordStamp(user))
            return Error.Validation("This reset link is invalid, already used or expired.");

        user.PasswordHash = hasher.Hash(request.NewPassword);
        // The link proved the person controls the mailbox, which is what verification checks too.
        if (user.Status == UserStatus.Pending)
            user.Status = UserStatus.Active;
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private Task<User?> FindByEmailAsync(string address, CancellationToken ct)
    {
        var normalized = address.Trim().ToLowerInvariant();
        return uow.Repository<User>().Query().FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    private async Task SendVerificationAsync(User user, CancellationToken ct)
    {
        var token = jwt.CreatePurposeToken(user.UserId, VerifyPurpose, EmailStamp(user), TimeSpan.FromDays(2));
        await email.SendAsync(user.Email, "Xác nhận email StorEase",
            $"Xin chào {user.FullName},\n\nMở liên kết sau để kích hoạt tài khoản (hết hạn sau 2 ngày):\n" +
            $"{app.FrontendBaseUrl}/verify-email?token={Uri.EscapeDataString(token)}", ct);
    }

    private static string EmailStamp(User user) => Stamp(user.Email.ToLowerInvariant());

    private static string PasswordStamp(User user) => Stamp(user.PasswordHash);

    private static string Stamp(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
