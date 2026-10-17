using StorEase.Application.Interfaces;

namespace StorEase.Infrastructure.Services;

/// <summary>Keeps uploads in one local folder (configured as <c>Storage:Root</c>, gitignored).</summary>
public class LocalFileStorage(string root) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var name = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var file = File.Create(Path.Combine(root, name));
        await content.CopyToAsync(file, ct);
        return name;
    }

    public Stream? OpenRead(string storedName)
    {
        // Stored names are generated here; anything with a path separator did not come from SaveAsync.
        if (storedName != Path.GetFileName(storedName))
            return null;
        var path = Path.Combine(root, storedName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }
}
