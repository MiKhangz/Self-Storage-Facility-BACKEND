namespace StorEase.Domain.Enums;

public enum UserStatus
{
    /// <summary>Registered online, email not verified yet (customers only).</summary>
    Pending,
    Active,
    /// <summary>Cannot sign in or be assigned work.</summary>
    Disabled
}
