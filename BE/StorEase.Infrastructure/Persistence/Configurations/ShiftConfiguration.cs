using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shift");
        builder.HasKey(x => x.ShiftId);

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId);
        builder.Property(x => x.ShiftType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
    }
}
