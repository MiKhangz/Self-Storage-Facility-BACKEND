using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class FacilityPriceVersionConfiguration : IEntityTypeConfiguration<FacilityPriceVersion>
{
    public void Configure(EntityTypeBuilder<FacilityPriceVersion> builder)
    {
        builder.ToTable("FacilityPriceVersion");
        builder.HasKey(x => x.FacilityPriceVersionId);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => new { x.FacilityId, x.Version }).IsUnique();
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FacilityUnitPriceConfiguration : IEntityTypeConfiguration<FacilityUnitPrice>
{
    public void Configure(EntityTypeBuilder<FacilityUnitPrice> builder)
    {
        builder.ToTable("FacilityUnitPrice");
        builder.HasKey(x => x.FacilityUnitPriceId);
        builder.HasIndex(x => new { x.FacilityPriceVersionId, x.UnitTypeId }).IsUnique();
        builder.HasOne(x => x.FacilityPriceVersion).WithMany(x => x.Prices).HasForeignKey(x => x.FacilityPriceVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.UnitType).WithMany().HasForeignKey(x => x.UnitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BasePricePerMonth).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Discount3Month).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Discount6Month).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Discount12Month).HasColumnType("decimal(5,2)");
    }
}
