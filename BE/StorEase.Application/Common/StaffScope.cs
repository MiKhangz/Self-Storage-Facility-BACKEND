using Microsoft.EntityFrameworkCore;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Common;

/// <summary>Which facilities a staff member may act on. Shared by tickets, maintenance, shifts and tasks.</summary>
public static class StaffScope
{
    /// <summary>
    /// Facilities the user is assigned to (<c>StaffFacility</c>) plus those they manage
    /// (<c>Facility.ManagerId</c>). Composable: use it as a subquery, e.g.
    /// <c>q.Where(t =&gt; ids.Contains(t.FacilityId))</c>.
    /// </summary>
    public static IQueryable<int> FacilityIdsOf(IUnitOfWork uow, int userId)
    {
        var assigned = uow.Repository<StaffFacility>().Query()
            .Where(s => s.UserId == userId)
            .Select(s => s.FacilityId);
        var managed = uow.Repository<Facility>().Query()
            .Where(f => f.ManagerId == userId)
            .Select(f => f.FacilityId);
        return assigned.Union(managed);
    }

    /// <summary>
    /// Facilities someone actually works at: an active manager, or an active facility staff member or manager
    /// assigned there. Elsewhere a customer's booking, handover or ticket would reach nobody.
    /// </summary>
    public static IQueryable<int> StaffedFacilityIds(IUnitOfWork uow)
    {
        var managed = uow.Repository<Facility>().Query()
            .Where(f => f.Manager != null && f.Manager.Status == UserStatus.Active)
            .Select(f => f.FacilityId);
        var assigned = uow.Repository<StaffFacility>().Query()
            .Where(s => s.User.Status == UserStatus.Active
                && (s.User.Role.RoleName == Roles.FacilityStaff || s.User.Role.RoleName == Roles.FacilityManager))
            .Select(s => s.FacilityId);
        return managed.Union(assigned);
    }

    /// <summary>
    /// Administrators and business managers see every facility; everyone else only their own scope.
    /// </summary>
    public static bool SeesAllFacilities(string role) =>
        role is Roles.SystemAdministrator or Roles.BusinessOperationsManager;

    /// <summary>
    /// Whether the caller may change the facility's setup (floors, units): administrators anywhere, a facility
    /// manager only where <see cref="FacilityIdsOf"/> includes it.
    /// </summary>
    public static async Task<bool> CanManageAsync(IUnitOfWork uow, ICurrentUser user, int facilityId, CancellationToken ct) =>
        user.Role == Roles.SystemAdministrator
        || (user.Role == Roles.FacilityManager
            && await FacilityIdsOf(uow, user.UserId).AnyAsync(id => id == facilityId, ct));
}
