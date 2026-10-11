using StorEase.Application.Common;

namespace StorEase.Application.Interfaces;

/// <summary>The caller, read from the JWT. Use it for ownership checks inside services.</summary>
public interface ICurrentUser
{
    int UserId { get; }
    string Role { get; }

    bool IsCustomer => Role == Roles.StorageCustomer;
}
