using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.ToTable("TicketCategory");
        builder.HasKey(x => x.CategoryId);

        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.DefaultPriority).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
    }
}
