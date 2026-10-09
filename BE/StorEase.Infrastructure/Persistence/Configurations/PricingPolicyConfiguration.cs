using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class PricingPolicyConfiguration : IEntityTypeConfiguration<PricingPolicy>
{
    public void Configure(EntityTypeBuilder<PricingPolicy> builder)
    {
        builder.ToTable("PricingPolicy");
        builder.HasKey(x => x.PricingPolicyId);

        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy);
        builder.Property(x => x.NoticeDays).HasDefaultValue(30);
        builder.Property(x => x.LateFeePercentPerWeek).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
