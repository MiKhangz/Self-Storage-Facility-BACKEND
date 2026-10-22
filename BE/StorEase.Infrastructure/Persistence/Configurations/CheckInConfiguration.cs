using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("CheckIn");
        builder.HasKey(x => x.CheckInId);

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedBy);
        builder.Property(x => x.Note).HasMaxLength(200);

        builder.HasIndex(x => x.ContractId);
        builder.HasIndex(x => x.StorageUnitId);
        builder.HasIndex(x => x.CheckInAt);
    }
}
