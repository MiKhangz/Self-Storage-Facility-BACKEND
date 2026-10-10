using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservation");
        builder.HasKey(x => x.ReservationId);

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId);
        builder.HasOne(x => x.UnitType).WithMany().HasForeignKey(x => x.UnitTypeId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy);
        builder.Property(x => x.ReservationCode).HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.MoveInDate);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReservationCode).IsUnique();
    }
}
