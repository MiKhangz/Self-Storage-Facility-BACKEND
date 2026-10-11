using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Auth.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}

public class AuthService(IUnitOfWork uow, IPasswordHasher hasher, IJwtService jwt) : IAuthService
{
    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await uow.Repository<User>().Query()
            .Where(u => u.Email == request.Email)
            .Select(u => new { u.UserId, u.FullName, u.Email, u.PasswordHash, u.Status, u.Role.RoleName })
            .FirstOrDefaultAsync(ct);

        // One message for unknown email and wrong password, so login cannot be used to probe for accounts.
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return Error.Unauthorized("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            return Error.Forbidden("This account is not active.");

        var token = jwt.CreateToken(user.UserId, user.FullName, user.Email, user.RoleName);
        return new LoginResponse(token, user.UserId, user.FullName, user.RoleName);
    }
}
