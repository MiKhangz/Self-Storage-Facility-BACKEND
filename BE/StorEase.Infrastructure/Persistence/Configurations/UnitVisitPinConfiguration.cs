using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public sealed class UnitVisitPinConfiguration : IEntityTypeConfiguration<UnitVisitPin>
{
    public void Configure(EntityTypeBuilder<UnitVisitPin> builder)
    {
        builder.ToTable("UnitVisitPin");
        builder.HasKey(x => x.UnitVisitPinId);
        builder.Property(x => x.Purpose).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsUnicode(false);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId);
        builder.HasOne(x => x.UnitVisit).WithMany().HasForeignKey(x => x.UnitVisitId);
        builder.HasIndex(x => new { x.CodeHash, x.Purpose, x.IsUsed });
        builder.HasIndex(x => new { x.UserId, x.ExpiredAt });
    }
}
