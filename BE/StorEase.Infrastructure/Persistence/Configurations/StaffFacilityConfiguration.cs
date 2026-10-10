using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class StaffFacilityConfiguration : IEntityTypeConfiguration<StaffFacility>
{
    public void Configure(EntityTypeBuilder<StaffFacility> builder)
    {
        builder.ToTable("StaffFacility");
        builder.HasKey(x => x.StaffFacilityId);

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId);
    }
}
