using StorEase.Domain.Enums;

namespace StorEase.Domain.Entities;

public class TicketCategory
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Priority DefaultPriority { get; set; }
}
