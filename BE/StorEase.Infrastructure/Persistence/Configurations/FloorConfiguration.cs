using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("Floor");
        builder.HasKey(x => x.FloorId);

        builder.HasOne(x => x.Facility).WithMany(x => x.Floors).HasForeignKey(x => x.FacilityId);
        builder.Property(x => x.Name).HasMaxLength(50);
    }
}
