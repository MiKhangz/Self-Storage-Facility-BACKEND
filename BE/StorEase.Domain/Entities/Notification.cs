namespace StorEase.Domain.Entities;

/// <summary>A message shown under the customer's bell. It is saved whether or not the email goes out.</summary>
public class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>The page on the customer site it is about, such as "/payments".</summary>
    public string Link { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public User User { get; set; } = null!;
}
