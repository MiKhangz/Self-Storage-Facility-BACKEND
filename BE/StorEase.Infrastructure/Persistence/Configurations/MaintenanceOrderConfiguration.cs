using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class MaintenanceOrderConfiguration : IEntityTypeConfiguration<MaintenanceOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceOrder> builder)
    {
        builder.ToTable("MaintenanceOrder");
        builder.HasKey(x => x.MaintenanceOrderId);

        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedTo);
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.Cost).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
