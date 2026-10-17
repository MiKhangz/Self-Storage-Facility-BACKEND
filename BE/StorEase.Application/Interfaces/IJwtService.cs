namespace StorEase.Application.Interfaces;

public interface IJwtService
{
    string CreateToken(int userId, string fullName, string email, string role);

    /// <summary>
    /// A short-lived signed token for one job (email verification, password reset). <paramref name="stamp"/> is
    /// a value the caller re-derives later; when it no longer matches (email changed, password already reset)
    /// the token stops working, which makes it single-use without storing anything.
    /// </summary>
    string CreatePurposeToken(int userId, string purpose, string stamp, TimeSpan lifetime);

    /// <summary>The user id and stamp of a valid, unexpired token for <paramref name="purpose"/>; null otherwise.</summary>
    (int UserId, string Stamp)? ReadPurposeToken(string token, string purpose);
}
