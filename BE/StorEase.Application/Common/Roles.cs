namespace StorEase.Application.Common;

/// <summary>Role names as stored in <c>Role.RoleName</c> and carried in the JWT <c>role</c> claim.</summary>
public static class Roles
{
    public const string StorageCustomer = "StorageCustomer";
    public const string FacilityStaff = "FacilityStaff";
    public const string FacilityManager = "FacilityManager";
    public const string BusinessOperationsManager = "BusinessOperationsManager";
    public const string SystemAdministrator = "SystemAdministrator";

    /// <summary>For <c>[Authorize(Roles = Roles.Staff)]</c>.</summary>
    public const string Staff = FacilityStaff + "," + FacilityManager;
}
