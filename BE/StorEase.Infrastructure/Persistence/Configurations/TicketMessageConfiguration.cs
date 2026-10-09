using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessage");
        builder.HasKey(x => x.MessageId);

        builder.HasOne(x => x.Ticket).WithMany(x => x.TicketMessages).HasForeignKey(x => x.TicketId);
        builder.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId);
        builder.Property(x => x.Body).HasMaxLength(1000);
    }
}
