using System.Security.Cryptography;
using System.Text;
using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Services;

/// <summary>AES-GCM: a random 12-byte nonce and a 16-byte tag around each code, stored as base64.</summary>
public sealed class AesCodeProtector(byte[] key) : ICodeProtector
{
    /// <summary>
    /// shortcut: the key is derived from the JWT signing secret, so rotating that secret makes stored codes unreadable:
    /// the desk still finds them by their hash, and customers issue a new one to see the digits. Give access codes their own key in settings before production.
    /// </summary>
    public static AesCodeProtector FromSecret(string secret) =>
        new(HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), 32, info: Encoding.UTF8.GetBytes("StorEase.AccessCode")));

    public string Protect(string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var output = new byte[12 + plain.Length + 16];
        var nonce = output.AsSpan(0, 12);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, output.AsSpan(12, plain.Length), output.AsSpan(12 + plain.Length));
        return Convert.ToBase64String(output);
    }

    public string? Unprotect(string protectedValue)
    {
        var input = Convert.FromBase64String(protectedValue);
        var plain = new byte[input.Length - 28];
        using var aes = new AesGcm(key, 16);
        try
        {
            aes.Decrypt(input.AsSpan(0, 12), input.AsSpan(12, plain.Length), input.AsSpan(12 + plain.Length), plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
        return Encoding.UTF8.GetString(plain);
    }
}
