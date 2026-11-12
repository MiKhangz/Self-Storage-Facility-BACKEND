namespace StorEase.Domain.Entities;

public class AccessCode
{
    public int AccessCodeId { get; set; }
    public int ContractId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    /// <summary>The code, encrypted, so its customer can read it again; null for codes issued before it was kept.</summary>
    public string? CodeCipher { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? SuspendedAt { get; set; }

    public Contract Contract { get; set; } = null!;
}
