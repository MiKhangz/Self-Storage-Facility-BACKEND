using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class HandoverConfiguration : IEntityTypeConfiguration<Handover>
{
    public void Configure(EntityTypeBuilder<Handover> builder)
    {
        builder.ToTable("Handover");
        builder.HasKey(x => x.HandoverId);

        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId);
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId);
        builder.Property(x => x.LockSerial).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CardNumber).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.ConditionNote).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
