namespace StorEase.Application.Interfaces;

/// <summary>Stores uploaded files outside the database; the database keeps only the returned name.</summary>
public interface IFileStorage
{
    /// <summary>Saves the stream under a new random name with <paramref name="extension"/> (e.g. ".jpg") and returns that name.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);

    /// <summary>Opens a stored file, or null when it does not exist.</summary>
    Stream? OpenRead(string storedName);
}
