namespace FileBlobEmulator.Services;

public class BlobFileBackend
{
    private readonly string _root;

    public BlobFileBackend(ILogger<BlobFileBackend> logger)
    {
        _root = Environment.GetEnvironmentVariable("BLOB_ROOT") ?? "blob-data";
        logger.LogInformation("Using blob storage root: {Root}", _root);
        Directory.CreateDirectory(_root);
    }


    private string A(string account) => Path.Combine(_root, account);
    private string C(string account, string container) => Path.Combine(A(account), container);
    private string B(string account, string container, string blob) => Path.Combine(C(account, container), blob);

    public void EnsureContainer(string account, string container)
        => Directory.CreateDirectory(C(account, container));

    public async Task SaveBlockAsync(string account, string container, string blobName, string blockId, Stream data)
    {
        var blockFolder = Path.Combine(B(account, container, blobName) + ".blocks");
        Directory.CreateDirectory(blockFolder);

        await using var fs = new FileStream(Path.Combine(blockFolder, blockId), FileMode.Create);
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
            var blockPath = Path.Combine(blockFolder, blockId);
            await using var src = new FileStream(blockPath, FileMode.Open);
            await src.CopyToAsync(dest);
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
        return true;
    }

    public IEnumerable<string> ListBlobs(string account, string container)
    {
        var path = C(account, container);
        if (!Directory.Exists(path)) yield break;

        foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            yield return Path.GetRelativePath(path, f);
        }
    }
}