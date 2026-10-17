using Microsoft.EntityFrameworkCore;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;

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
