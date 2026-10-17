using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class HandoverPhotoConfiguration : IEntityTypeConfiguration<HandoverPhoto>
{
    public void Configure(EntityTypeBuilder<HandoverPhoto> builder)
    {
        builder.ToTable("HandoverPhoto");
        builder.HasKey(x => x.HandoverPhotoId);

        builder.HasOne(x => x.Handover).WithMany().HasForeignKey(x => x.HandoverId);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy);
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.StoredName).HasMaxLength(100).IsUnicode(false);
    }
}
