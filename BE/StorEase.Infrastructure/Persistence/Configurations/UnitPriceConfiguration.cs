using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class UnitPriceConfiguration : IEntityTypeConfiguration<UnitPrice>
{
    public void Configure(EntityTypeBuilder<UnitPrice> builder)
    {
        builder.ToTable("UnitPrice");
        builder.HasKey(x => x.UnitPriceId);

        builder.HasOne(x => x.PricingPolicy).WithMany(x => x.UnitPrices).HasForeignKey(x => x.PricingPolicyId);
        builder.HasOne(x => x.UnitType).WithMany().HasForeignKey(x => x.UnitTypeId);
        builder.Property(x => x.BasePricePerMonth).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Discount3Month).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Discount6Month).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Discount12Month).HasColumnType("decimal(5,2)");
    }
}
