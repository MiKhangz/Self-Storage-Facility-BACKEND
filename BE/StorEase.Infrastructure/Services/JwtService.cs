using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Services;

public class JwtService(JwtOptions options) : IJwtService
{
    public string CreateToken(int userId, string fullName, string email, string role)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim("sub", userId.ToString()),
                new Claim("name", fullName),
                new Claim("email", email),
                new Claim("role", role),
            ],
            expires: DateTime.UtcNow.AddMinutes(options.ExpiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
