namespace StorEase.Domain.Entities;

public class CheckInPhoto
{
    public int CheckInPhotoId { get; set; }
    public int CheckInId { get; set; }
    public int UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }

    public CheckIn CheckIn { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
