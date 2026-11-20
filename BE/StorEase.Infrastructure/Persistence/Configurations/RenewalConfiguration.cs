using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class RenewalConfiguration : IEntityTypeConfiguration<Renewal>
{
    public void Configure(EntityTypeBuilder<Renewal> builder)
    {
        builder.ToTable("Renewal");
        builder.HasKey(x => x.RenewalId);

        builder.HasOne(x => x.Contract).WithMany(x => x.Renewals).HasForeignKey(x => x.ContractId);
        builder.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedBy);
        builder.HasOne(x => x.TransferStorageUnit).WithMany().HasForeignKey(x => x.TransferStorageUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.NewMonthlyRent).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
    }
}
