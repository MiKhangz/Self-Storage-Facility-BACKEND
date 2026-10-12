using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using StorEase.Application.Common;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Auth.Dtos;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;
using StorEase.Infrastructure.Services;

namespace StorEase.Application.Tests;

/// <summary>Uses the real PBKDF2 hasher and JWT service, so a password stored by the seeder verifies the same way.</summary>
public class AuthServiceTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new();

    private static readonly JwtService Jwt = new(new JwtOptions
    {
        Issuer = "StorEase", Audience = "StorEase.Frontend", Key = new string('k', 64), ExpiresMinutes = 60,
    });

    private static TestDb WithUser(UserStatus status = UserStatus.Active)
    {
        var t = new TestDb();
        t.Db.Add(new User
        {
            Role = new Role { RoleName = Roles.FacilityStaff }, FullName = "Nhân viên", Email = "staff@x.vn",
            PhoneNumber = "0900000000", PasswordHash = Hasher.Hash("Demo@123"), Status = status, CreatedAt = DateTime.UtcNow,
        });
        t.Db.SaveChanges();
        return t;
    }

    private static Task<Result<LoginResponse>> Login(TestDb t, string email, string password) =>
        new AuthService(t.Uow, Hasher, Jwt).LoginAsync(new LoginRequest(email, password), default);

    [Fact]
    public async Task Correct_credentials_return_a_token_carrying_the_user_and_role()
    {
        using var t = WithUser();

        var result = await Login(t, "staff@x.vn", "Demo@123");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Role.Should().Be(Roles.FacilityStaff);
        result.Value.FullName.Should().Be("Nhân viên");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);
        token.Claims.Should().Contain(c => c.Type == "role" && c.Value == Roles.FacilityStaff);
        token.Claims.Should().Contain(c => c.Type == "sub" && c.Value == result.Value.UserId.ToString());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_the_same_401()
    {
        using var t = WithUser();

        var wrongPassword = await Login(t, "staff@x.vn", "nope");
        var unknownEmail = await Login(t, "nobody@x.vn", "Demo@123");

        wrongPassword.Error!.Type.Should().Be(ErrorType.Unauthorized);
        unknownEmail.Error!.Message.Should().Be(wrongPassword.Error.Message, "the answer must not reveal which accounts exist");
    }

    [Fact]
    public async Task An_inactive_account_cannot_sign_in()
    {
        using var t = WithUser(UserStatus.Suspended);

        var result = await Login(t, "staff@x.vn", "Demo@123");

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Password_hashes_are_salted_and_verify()
    {
        var first = Hasher.Hash("Demo@123");
        var second = Hasher.Hash("Demo@123");

        first.Should().NotBe(second, "each hash has its own salt");
        Hasher.Verify("Demo@123", first).Should().BeTrue();
        Hasher.Verify("demo@123", first).Should().BeFalse();
        Hasher.Verify("Demo@123", "not-a-hash").Should().BeFalse();
    }
}
