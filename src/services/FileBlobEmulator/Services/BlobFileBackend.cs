using System.Text.RegularExpressions;

namespace FileBlobEmulator.Services;

public partial class BlobFileBackend
{
    private readonly string _root;

    public BlobFileBackend(ILogger<BlobFileBackend> logger)
    {
        _root = Environment.GetEnvironmentVariable("BLOB_ROOT") ?? "blob-data";
        logger.LogInformation("Using blob storage root: {Root}", _root);
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// Sanitize path segment to prevent path traversal attacks.
    /// Only allows alphanumeric, dash, underscore, and dot characters.
    /// </summary>
    private static string SanitizePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Path segment cannot be empty", nameof(input));

        // Remove any path traversal attempts and invalid characters
        var sanitized = PathSanitizeRegex().Replace(input, "");

        // Ensure the sanitized path is not empty after cleaning
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Path segment contains only invalid characters", nameof(input));

        // Prevent hidden files (starting with dot) unless it's a file extension
        if (sanitized.StartsWith('.'))
            sanitized = "_" + sanitized[1..];

        return sanitized;
    }

    /// <summary>
    /// Validates that the final path is within the allowed root directory.
    /// </summary>
    private void ValidatePathWithinRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetFullPath(_root);

        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access to path outside of blob root is not allowed");
    }

    private string A(string account)
    {
        var safe = SanitizePath(account);
        var path = Path.Combine(_root, safe);
        ValidatePathWithinRoot(path);
        return path;
    }

    private string C(string account, string container)
    {
        var safeContainer = SanitizePath(container);
        var path = Path.Combine(A(account), safeContainer);
        ValidatePathWithinRoot(path);
        return path;
    }

    private string B(string account, string container, string blob)
    {
        // For blob paths that may contain subdirectories (e.g., "folder/file.txt"),
        // sanitize each segment individually
        var segments = blob.Split(new[] { '/', '\\' }, StringSplitOptions.None);
        var safeSegments = segments.Select(SanitizePath).ToArray();
        var safeBlob = Path.Combine(safeSegments);

        var path = Path.Combine(C(account, container), safeBlob);
        ValidatePathWithinRoot(path);
        return path;
    }

    private static string SanitizeBlockId(string blockId)
    {
        // Block IDs need stricter sanitization
        var safe = BlockIdSanitizeRegex().Replace(blockId, "");
        if (string.IsNullOrWhiteSpace(safe))
            throw new ArgumentException("Block ID contains only invalid characters", nameof(blockId));
        return safe;
    }

    public void EnsureContainer(string account, string container)
        => Directory.CreateDirectory(C(account, container));

    public void DeleteContainer(string account, string container)
    {
        var containerPath = C(account, container);
        if (Directory.Exists(containerPath))
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    public async Task SaveBlockAsync(string account, string container, string blobName, string blockId, Stream data)
    {
        var safeBlockId = SanitizeBlockId(blockId);
        var blockFolder = B(account, container, blobName) + ".blocks";
        Directory.CreateDirectory(blockFolder);

        var blockPath = Path.Combine(blockFolder, safeBlockId);
        ValidatePathWithinRoot(blockPath);

        await using var fs = new FileStream(blockPath, FileMode.Create);
        await data.CopyToAsync(fs);
    }

    public async Task CommitBlocksAsync(string account, string container, string blobName, List<string> blockIds)
    {
        EnsureContainer(account, container);

        var finalPath = B(account, container, blobName);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        await using var dest = new FileStream(finalPath, FileMode.Create);

        var blockFolder = finalPath + ".blocks";

        foreach (var blockId in blockIds)
        {
            var safeBlockId = SanitizeBlockId(blockId);
            var blockPath = Path.Combine(blockFolder, safeBlockId);
            ValidatePathWithinRoot(blockPath);

            await using var src = new FileStream(blockPath, FileMode.Open);
            await src.CopyToAsync(dest);
        }

        // Cleanup: delete block folder after successful commit
        if (Directory.Exists(blockFolder))
        {
            Directory.Delete(blockFolder, recursive: true);
        }
    }

    public Stream? GetBlob(string account, string container, string blobName)
    {
        var p = B(account, container, blobName);
        return File.Exists(p) ? new FileStream(p, FileMode.Open, FileAccess.Read) : null;
    }

    public bool DeleteBlob(string account, string container, string blobName)
    {
        var p = B(account, container, blobName);
        if (!File.Exists(p)) return false;

        File.Delete(p);

        // Also delete any leftover block folder
        var blockFolder = p + ".blocks";
        if (Directory.Exists(blockFolder))
        {
            Directory.Delete(blockFolder, recursive: true);
        }

        return true;
    }

    public IEnumerable<string> ListBlobs(string account, string container)
    {
        var path = C(account, container);
        if (!Directory.Exists(path)) yield break;

        foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            // Skip block folders
            if (f.Contains(".blocks")) continue;
            yield return Path.GetRelativePath(path, f);
        }
    }

    // Regex patterns for path sanitization - compiled for performance
    [GeneratedRegex(@"[^a-zA-Z0-9\-_\.]")]
    private static partial Regex PathSanitizeRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9\-_]")]
    private static partial Regex BlockIdSanitizeRegex();
}