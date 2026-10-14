namespace StorEase.Domain.Entities;

/// <summary>One row per user who has changed the defaults; a user without a row gets the defaults.</summary>
public class NotificationPreference
{
    public int NotificationPreferenceId { get; set; }
    public int UserId { get; set; }
    public bool EmailReminders { get; set; } = true;
    public bool SmsReminders { get; set; }
    public bool MarketingEmails { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
