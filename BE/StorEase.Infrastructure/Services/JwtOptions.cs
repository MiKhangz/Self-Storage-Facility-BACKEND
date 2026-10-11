namespace StorEase.Infrastructure.Services;

/// <summary>Bound from the <c>Jwt</c> config section. <see cref="Key"/> lives only in the gitignored appsettings.Development.json.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public int ExpiresMinutes { get; init; } = 480;
}
