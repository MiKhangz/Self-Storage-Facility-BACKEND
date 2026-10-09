using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItem");
        builder.HasKey(x => x.InvoiceItemId);

        builder.HasOne(x => x.Invoice).WithMany(x => x.InvoiceItems).HasForeignKey(x => x.InvoiceId);
        builder.HasOne(x => x.FeeType).WithMany().HasForeignKey(x => x.FeeTypeId);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.UnitAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.LineAmount).HasColumnType("decimal(12,2)");
    }
}
