namespace StorEase.Application.Interfaces;

/// <summary>Reversible encryption for access codes, so a customer can see their current code in their account.</summary>
public interface ICodeProtector
{
    string Protect(string value);
    /// <summary>The value, or null when it cannot be read (it was encrypted with a key since replaced).</summary>
    string? Unprotect(string protectedValue);
}
