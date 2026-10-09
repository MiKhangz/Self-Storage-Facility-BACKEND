using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class StorageUnitConfiguration : IEntityTypeConfiguration<StorageUnit>
{
    public void Configure(EntityTypeBuilder<StorageUnit> builder)
    {
        builder.ToTable("StorageUnit");
        builder.HasKey(x => x.StorageUnitId);

        builder.HasOne(x => x.Facility).WithMany(x => x.StorageUnits).HasForeignKey(x => x.FacilityId);
        builder.HasOne(x => x.Floor).WithMany().HasForeignKey(x => x.FloorId);
        builder.HasOne(x => x.UnitType).WithMany().HasForeignKey(x => x.UnitTypeId);
        builder.Property(x => x.UnitCode).HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.UnitCode);
        builder.Property(x => x.Zone).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.CurrentPrice).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Note).HasMaxLength(200);
        builder.HasIndex(x => new { x.FacilityId, x.UnitCode }).IsUnique();
    }
}
