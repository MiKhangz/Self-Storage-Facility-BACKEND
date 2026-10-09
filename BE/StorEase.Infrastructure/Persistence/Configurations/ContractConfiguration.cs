using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contract");
        builder.HasKey(x => x.ContractId);

        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.Property(x => x.ContractNumber).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.MonthlyRent).HasColumnType("decimal(12,2)");
        builder.Property(x => x.DepositAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ContractNumber).IsUnique();
    }
}
