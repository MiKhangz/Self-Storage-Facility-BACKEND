using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class UnitReturnConfiguration : IEntityTypeConfiguration<UnitReturn>
{
    public void Configure(EntityTypeBuilder<UnitReturn> builder)
    {
        builder.ToTable("UnitReturn");
        builder.HasKey(x => x.UnitReturnId);

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.Property(x => x.RefundMethod).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.RefundAccount).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
