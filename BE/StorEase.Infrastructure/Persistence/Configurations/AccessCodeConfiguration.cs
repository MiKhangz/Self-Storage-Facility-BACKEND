using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class AccessCodeConfiguration : IEntityTypeConfiguration<AccessCode>
{
    public void Configure(EntityTypeBuilder<AccessCode> builder)
    {
        builder.ToTable("AccessCode");
        builder.HasKey(x => x.AccessCodeId);

        builder.HasOne(x => x.Contract).WithMany(x => x.AccessCodes).HasForeignKey(x => x.ContractId);
        builder.Property(x => x.CodeHash).HasMaxLength(255).IsUnicode(false);
    }
}
