namespace StorEase.Domain.Enums;

public enum FloorStatus
{
    Open,
    UnderConstruction,
    Renovation,
    /// <summary>Taken out of use while its units still have history; hidden from the facility.</summary>
    Removed
}
