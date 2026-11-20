namespace StorEase.Application.Features.Renewals;

/// <summary>Renewal.Status is a varchar(20) column; these are its allowed values.</summary>
public static class RenewalStatus
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    /// <summary>Approved only if the renter agrees to move: the next customer booked their unit, so another is offered.</summary>
    public const string AwaitingCustomer = "AwaitingCustomer";
}
