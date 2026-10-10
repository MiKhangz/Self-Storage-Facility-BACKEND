using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class UnitTypeConfiguration : IEntityTypeConfiguration<UnitType>
{
    public void Configure(EntityTypeBuilder<UnitType> builder)
    {
        builder.ToTable("UnitType");
        builder.HasKey(x => x.UnitTypeId);

        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.AreaM2).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Width).HasColumnType("decimal(4,2)");
        builder.Property(x => x.Depth).HasColumnType("decimal(4,2)");
        builder.Property(x => x.Height).HasColumnType("decimal(4,2)");
    }
}
