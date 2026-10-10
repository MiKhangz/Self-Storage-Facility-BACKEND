using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment");
        builder.HasKey(x => x.PaymentId);

        builder.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId);
        builder.HasOne(x => x.ReceivedByUser).WithMany().HasForeignKey(x => x.ReceivedBy);
        builder.Property(x => x.PaymentCode).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.ReferenceNo).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PaymentCode).IsUnique();
    }
}
