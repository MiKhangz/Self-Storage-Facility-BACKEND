using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        builder.HasKey(x => x.UserId);

        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        builder.Property(x => x.FullName).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false);
        builder.Property(x => x.CitizenId).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Address).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Email).IsUnique();
    }
}
