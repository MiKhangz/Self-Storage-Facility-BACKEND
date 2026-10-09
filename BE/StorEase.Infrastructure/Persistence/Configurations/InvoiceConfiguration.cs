using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoice");
        builder.HasKey(x => x.InvoiceId);

        builder.HasOne(x => x.Contract).WithMany(x => x.Invoices).HasForeignKey(x => x.ContractId);
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.DueDate);
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.PaidAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
    }
}
