using StorEase.Application.Common;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets;

/// <summary>The one transition table for staff-driven ticket status changes, shared by the queue and the conversation.</summary>
internal static class TicketStatusRules
{
    /// <summary>Null when <paramref name="role"/> may move a ticket from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Error? CheckMove(string role, bool isCustomer, SupportTicketStatus from, SupportTicketStatus to)
    {
        if (from == SupportTicketStatus.Closed)
            return Error.Conflict("A Closed ticket cannot change status.");
        if (from == to)
            return Error.Conflict($"The ticket is already {to}.");

        // Who may reach each target: the owning customer or a manager closes; staff do the rest.
        if (to == SupportTicketStatus.Closed)
            return !isCustomer && role != Roles.FacilityManager
                ? Error.Forbidden("Only the customer or a facility manager can close a ticket.")
                : null;
        if (isCustomer)
            return Error.Forbidden("A customer can only close their own ticket.");
        return AllowedStaffMove(from, to) ? null : Error.Conflict($"A {from} ticket cannot move to {to}.");
    }

    private static bool AllowedStaffMove(SupportTicketStatus from, SupportTicketStatus to) => (from, to) switch
    {
        (SupportTicketStatus.New, SupportTicketStatus.InProgress) => true,
        (SupportTicketStatus.InProgress, SupportTicketStatus.WaitingForCustomer) => true,
        (SupportTicketStatus.WaitingForCustomer, SupportTicketStatus.InProgress) => true,
        (SupportTicketStatus.New or SupportTicketStatus.InProgress or SupportTicketStatus.WaitingForCustomer,
            SupportTicketStatus.Resolved) => true,
        _ => false,
    };
}
