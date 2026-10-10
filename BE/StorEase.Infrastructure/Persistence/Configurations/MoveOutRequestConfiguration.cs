using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class MoveOutRequestConfiguration : IEntityTypeConfiguration<MoveOutRequest>
{
    public void Configure(EntityTypeBuilder<MoveOutRequest> builder)
    {
        builder.ToTable("MoveOutRequest");
        builder.HasKey(x => x.MoveOutRequestId);

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.Property(x => x.RefundMethod).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.RefundAccount).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
