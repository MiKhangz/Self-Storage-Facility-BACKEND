using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? CitizenId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public UserStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
}
