using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class OverdueCaseConfiguration : IEntityTypeConfiguration<OverdueCase>
{
    public void Configure(EntityTypeBuilder<OverdueCase> builder)
    {
        builder.ToTable("OverdueCase");
        builder.HasKey(x => x.OverdueCaseId);

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId);
        builder.Property(x => x.LateFeeAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Stage).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(x => x.Stage);
    }
}
