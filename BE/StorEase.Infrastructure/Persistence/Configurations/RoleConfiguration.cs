using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");
        builder.HasKey(x => x.RoleId);

        builder.Property(x => x.RoleName).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.Description).HasMaxLength(200).IsUnicode(false);
    }
}
