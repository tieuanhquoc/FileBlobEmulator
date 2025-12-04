# FileBlobEmulator

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=tieuanhquoc_FileBlobEmulator&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=tieuanhquoc_FileBlobEmulator)
[![CodeQL Advanced](https://github.com/tieuanhquoc/FileBlobEmulator/actions/workflows/codeql.yml/badge.svg)](https://github.com/tieuanhquoc/FileBlobEmulator/actions/workflows/codeql.yml)



A lightweight Azure Blob Storage emulator that stores blobs directly on the file system. Designed as a faster alternative to Azurite for local development and testing.

## Features

- ✅ **File-based storage** - Blobs stored directly as files, not JSON
- ✅ **Azure Blob API compatible** - Works with Azure SDK
- ✅ **SharedKey authentication** - Full HMAC-SHA256 signature validation
- ✅ **Block blob support** - Single-shot and block upload
- ✅ **Container operations** - Create, delete, list blobs
- ✅ **Swagger UI** - API documentation in development mode

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Docker (optional)

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `BLOB_ACCOUNT_NAME` | Storage account name | ✅ Yes |
| `BLOB_ACCOUNT_KEY` | Base64-encoded account key | ✅ Yes |
| `BLOB_ROOT` | Root directory for blob storage | No (default: `blob-data`) |

### Run Locally

```bash
# Set environment variables
export BLOB_ACCOUNT_NAME=devstoreaccount1
export BLOB_ACCOUNT_KEY=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==

# Run the application
cd src/services/FileBlobEmulator
dotnet run
```

The server will start at `https://localhost:5001` (or `http://localhost:5000`).

### Run with Docker (Recommended)

```bash
docker run -d \
  -p 5000:8080 \
  -e BLOB_ACCOUNT_NAME=devstoreaccount1 \
  -e BLOB_ACCOUNT_KEY=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw== \
  -v $(pwd)/blob-data:/app/blob-data \
  tieuanhquoc/fileblobemulator:latest
```

### Build from Source

```bash
# Clone and build
docker build -t fileblobemulator .

# Or run with .NET directly
cd src/services/FileBlobEmulator
dotnet run
```

## API Endpoints

### Container Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| `PUT` | `/{account}/{container}?restype=container` | Create container |
| `DELETE` | `/{account}/{container}?restype=container` | Delete container |
| `GET` | `/{account}/{container}?restype=container&comp=list` | List blobs |

### Blob Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| `PUT` | `/{account}/{container}/{blob}` | Upload blob (single-shot) |
| `PUT` | `/{account}/{container}/{blob}?comp=block&blockid=...` | Upload block |
| `PUT` | `/{account}/{container}/{blob}?comp=blocklist` | Commit blocks |
| `GET` | `/{account}/{container}/{blob}` | Download blob |
| `DELETE` | `/{account}/{container}/{blob}` | Delete blob |

## Usage with Azure SDK

```csharp
using Azure.Storage.Blobs;

var connectionString = "DefaultEndpointsProtocol=http;" +
    "AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    "BlobEndpoint=http://localhost:5000/devstoreaccount1";

var client = new BlobServiceClient(connectionString);
var container = client.GetBlobContainerClient("mycontainer");
await container.CreateIfNotExistsAsync();

var blob = container.GetBlobClient("myblob.txt");
await blob.UploadAsync(BinaryData.FromString("Hello, World!"));
```

## Development

### Swagger UI

Access Swagger UI at `https://localhost:5001/swagger` (development mode only).

### Logs

Logs are written to:
- Console (all levels)
- `logs/blobserver-{date}.log` (Warning+ as JSON)

## License

Apache License 2.0 - See [LICENSE](LICENSE) for details.
