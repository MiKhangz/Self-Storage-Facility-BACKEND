using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class DepositRefundConfiguration : IEntityTypeConfiguration<DepositRefund>
{
    public void Configure(EntityTypeBuilder<DepositRefund> builder)
    {
        builder.ToTable("DepositRefund");
        builder.HasKey(x => x.DepositRefundId);

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UnitReturn).WithMany().HasForeignKey(x => x.UnitReturnId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PaidByUser).WithMany().HasForeignKey(x => x.PaidBy).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.RefundAccount).HasMaxLength(100);
        builder.Property(x => x.ReferenceNo).HasMaxLength(50);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.FacilityId);
        // One refund per cancelled reservation and one per settled return, so a retried request cannot pay twice.
        builder.HasIndex(x => new { x.ReservationId, x.Source }).IsUnique().HasFilter("[Source] = 'Cancellation'");
        builder.HasIndex(x => x.UnitReturnId).IsUnique().HasFilter("[UnitReturnId] IS NOT NULL");
    }
}
