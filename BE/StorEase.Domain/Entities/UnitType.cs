namespace StorEase.Domain.Entities;

public class UnitType
{
    public int UnitTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AreaM2 { get; set; }
    public decimal Width { get; set; }
    public decimal Depth { get; set; }
    public decimal Height { get; set; }
    public bool IsClimateControlled { get; set; }
}
