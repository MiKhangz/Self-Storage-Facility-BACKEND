using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class AccessLogConfiguration : IEntityTypeConfiguration<AccessLog>
{
    public void Configure(EntityTypeBuilder<AccessLog> builder)
    {
        builder.ToTable("AccessLog");
        builder.HasKey(x => x.AccessLogId);

        builder.HasOne(x => x.AccessCode).WithMany().HasForeignKey(x => x.AccessCodeId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.DeviceId).HasMaxLength(30).IsUnicode(false);
    }
}
