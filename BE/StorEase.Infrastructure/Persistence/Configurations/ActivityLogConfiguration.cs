using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLog");
        builder.HasKey(x => x.LogId);

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId);
        builder.Property(x => x.Action).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.EntityName).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.EntityId).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Result);
    }
}
