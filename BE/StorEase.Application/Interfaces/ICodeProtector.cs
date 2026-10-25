namespace StorEase.Application.Interfaces;

/// <summary>Reversible encryption for access codes, so a customer can see their current code in their account.</summary>
public interface ICodeProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
