namespace FileBlobEmulator.Models;

public record BlobInfo(
    string Name,
    long Length,
    DateTimeOffset LastModified
);
