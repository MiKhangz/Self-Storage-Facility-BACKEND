namespace StorEase.Domain.Enums;

/// <summary>How a visit ended.</summary>
public enum CheckOutMethod
{
    /// <summary>The customer said their code at the desk on the way out.</summary>
    Desk,
    /// <summary>Staff closed it by hand, with a reason (the customer left without checking out).</summary>
    MarkedByStaff,
    /// <summary>Still open at the facility's closing time, so the system closed it.</summary>
    AutoAtClosing
}
