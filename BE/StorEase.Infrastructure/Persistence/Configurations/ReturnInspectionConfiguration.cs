using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class ReturnInspectionConfiguration : IEntityTypeConfiguration<ReturnInspection>
{
    public void Configure(EntityTypeBuilder<ReturnInspection> builder)
    {
        builder.ToTable("ReturnInspection");
        builder.HasKey(x => x.ReturnInspectionId);

        builder.HasOne(x => x.MoveOutRequest).WithMany().HasForeignKey(x => x.MoveOutRequestId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId);
        builder.Property(x => x.ConditionResult).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CleaningFee).HasColumnType("decimal(12,2)");
        builder.Property(x => x.RepairCharge).HasColumnType("decimal(12,2)");
        builder.Property(x => x.OtherDeduction).HasColumnType("decimal(12,2)");
        builder.Property(x => x.RefundAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
