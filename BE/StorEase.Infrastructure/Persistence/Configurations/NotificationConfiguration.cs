using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.HasKey(x => x.NotificationId);

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Body).HasMaxLength(1000);
        builder.Property(x => x.Link).HasMaxLength(200);
        // The bell asks for one user's newest, every 15 seconds.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
