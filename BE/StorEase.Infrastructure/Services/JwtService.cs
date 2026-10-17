using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Services;

public class JwtService(JwtOptions options) : IJwtService
{
    private SigningCredentials Credentials => new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);

    public string CreateToken(int userId, string fullName, string email, string role)
    {
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
            signingCredentials: Credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // A distinct audience per purpose keeps a reset token from ever being accepted as a login token or vice versa.
    public string CreatePurposeToken(int userId, string purpose, string stamp, TimeSpan lifetime)
    {
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: PurposeAudience(purpose),
            claims: [new Claim("sub", userId.ToString()), new Claim("stamp", stamp)],
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: Credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (int UserId, string Stamp)? ReadPurposeToken(string token, string purpose)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = options.Issuer,
                ValidAudience = PurposeAudience(purpose),
                IssuerSigningKey = Credentials.Key,
                ClockSkew = TimeSpan.FromMinutes(1),
            }, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var stamp = principal.FindFirst("stamp")?.Value;
            return int.TryParse(sub, out var id) && stamp is not null ? (id, stamp) : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private string PurposeAudience(string purpose) => $"{options.Audience}:{purpose}";
}
