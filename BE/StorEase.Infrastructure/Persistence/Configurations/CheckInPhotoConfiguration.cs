using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence.Configurations;

public class CheckInPhotoConfiguration : IEntityTypeConfiguration<CheckInPhoto>
{
    public void Configure(EntityTypeBuilder<CheckInPhoto> builder)
    {
        builder.ToTable("CheckInPhoto");
        builder.HasKey(x => x.CheckInPhotoId);

        builder.HasOne(x => x.CheckIn).WithMany().HasForeignKey(x => x.CheckInId);
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy);
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.StoredName).HasMaxLength(100).IsUnicode(false);
    }
}
