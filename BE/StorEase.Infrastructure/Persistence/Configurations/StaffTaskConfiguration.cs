using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class StaffTaskConfiguration : IEntityTypeConfiguration<StaffTask>
{
    public void Configure(EntityTypeBuilder<StaffTask> builder)
    {
        builder.ToTable("StaffTask");
        builder.HasKey(x => x.TaskId);

        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedTo);
        builder.Property(x => x.TaskType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.ReferenceCode).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
    }
}
