namespace StorEase.Application.Interfaces;

/// <summary>One-way hash for passwords and access codes. Store <see cref="Hash"/>'s output, never the plaintext.</summary>
public interface IPasswordHasher
{
    string Hash(string value);
    bool Verify(string value, string hash);
}
