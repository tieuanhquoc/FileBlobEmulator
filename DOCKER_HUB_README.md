# FileBlobEmulator

A lightweight **Azure Blob Storage emulator** that stores blobs directly on the file system. Designed as a faster, simpler alternative to Azurite for local development and testing.

## Why FileBlobEmulator?

- 🚀 **Fast** - File-based storage, no JSON serialization overhead
- ✅ **Azure SDK compatible** - Works with official Azure Storage SDK
- 🔐 **Full authentication** - SharedKey with HMAC-SHA256 validation
- 📦 **Block blob support** - Single-shot and chunked uploads
- 🐳 **Lightweight** - ~100MB Docker image

## Quick Start

```bash
docker run -d \
  -p 5000:8080 \
  -e BLOB_ACCOUNT_NAME=devstoreaccount1 \
  -e BLOB_ACCOUNT_KEY=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw== \
  -v $(pwd)/blob-data:/app/blob-data \
  tieuanhquoc/fileblobemulator:latest
```

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `BLOB_ACCOUNT_NAME` | Storage account name | ✅ Yes |
| `BLOB_ACCOUNT_KEY` | Base64-encoded account key | ✅ Yes |
| `BLOB_ROOT` | Blob storage directory | No (default: `/app/blob-data`) |

## Using with Azure SDK

```csharp
var connectionString = "DefaultEndpointsProtocol=http;" +
    "AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    "BlobEndpoint=http://localhost:5000/devstoreaccount1";

var client = new BlobServiceClient(connectionString);
```

## Supported Operations

### Container
- `PUT /{account}/{container}?restype=container` - Create
- `DELETE /{account}/{container}?restype=container` - Delete
- `GET /{account}/{container}?restype=container&comp=list` - List blobs

### Blob
- `PUT /{account}/{container}/{blob}` - Upload
- `PUT /{account}/{container}/{blob}?comp=block&blockid=...` - Upload block
- `PUT /{account}/{container}/{blob}?comp=blocklist` - Commit blocks
- `GET /{account}/{container}/{blob}` - Download
- `DELETE /{account}/{container}/{blob}` - Delete

## Persistent Storage

Mount a volume to persist blobs:

```bash
docker run -v /path/to/storage:/app/blob-data tieuanhquoc/fileblobemulator
```

## Tags

- `latest` - Latest stable release
- `v0.0.1` - Initial release

## Source Code

GitHub: [github.com/tieuanhquoc/FileBlobEmulator](https://github.com/tieuanhquoc/FileBlobEmulator)

## License

Apache License 2.0
