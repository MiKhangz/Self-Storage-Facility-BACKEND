using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTicket");
        builder.HasKey(x => x.TicketId);

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.StorageUnit).WithMany().HasForeignKey(x => x.StorageUnitId);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedTo);
        builder.Property(x => x.TicketCode).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.TicketCode).IsUnique();
    }
}
