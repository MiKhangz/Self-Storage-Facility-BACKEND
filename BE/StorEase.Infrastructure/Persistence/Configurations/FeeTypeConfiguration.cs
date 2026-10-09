using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class FeeTypeConfiguration : IEntityTypeConfiguration<FeeType>
{
    public void Configure(EntityTypeBuilder<FeeType> builder)
    {
        builder.ToTable("FeeType");
        builder.HasKey(x => x.FeeTypeId);

        builder.HasOne(x => x.PricingPolicy).WithMany(x => x.FeeTypes).HasForeignKey(x => x.PricingPolicyId);
        builder.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
    }
}
