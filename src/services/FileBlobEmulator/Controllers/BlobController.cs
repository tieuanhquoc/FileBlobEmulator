using System.Text;
using System.Xml.Linq;
using FileBlobEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileBlobEmulator.Controllers;

[ApiController]
[Route("{account}/{container}")]
public class BlobController : ControllerBase
{
    private readonly BlobFileBackend _backend;
    private readonly ILogger<BlobController> _logger;

    public BlobController(BlobFileBackend backend, ILogger<BlobController> logger)
    {
        _backend = backend;
        _logger = logger;
    }

    // ================================
    // CONTAINER OPERATIONS
    // ================================

    // PUT /{account}/{container}?restype=container
    [HttpPut]
    public IActionResult CreateContainer(
        string account,
        string container,
        [FromQuery] string? restype)
    {
        if (!string.Equals(restype, "container", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid restype");

        _logger.LogInformation("Create container {Account}/{Container}", account, container);

        _backend.EnsureContainer(account, container);
        return Created($"/{account}/{container}", null);
    }

    // DELETE /{account}/{container}?restype=container
    [HttpDelete]
    public IActionResult DeleteContainer(
        string account,
        string container,
        [FromQuery] string? restype)
    {
        if (!string.Equals(restype, "container", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid restype");

        _logger.LogInformation("Delete container {Account}/{Container}", account, container);

        var containerPath = Path.Combine("blob-data", account, container);
        if (Directory.Exists(containerPath))
        {
            Directory.Delete(containerPath, recursive: true);
        }

        return Accepted();
    }

    // GET /{account}/{container}?restype=container&comp=list
    [HttpGet]
    public IActionResult ListBlobs(
        string account,
        string container,
        [FromQuery] string? restype,
        [FromQuery] string? comp)
    {
        if (!string.Equals(restype, "container", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(comp, "list", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid query parameters");
        }

        var blobs = _backend.ListBlobs(account, container).ToList();

        var xml = new XElement("EnumerationResults",
            new XAttribute("ContainerName", $"{account}/{container}"),
            new XElement("Blobs",
                blobs.Select(b => new XElement("Blob",
                    new XElement("Name", b)
                ))
            ),
            new XElement("NextMarker", "")
        );

        return Content(xml.ToString(), "application/xml");
    }

    // ================================
    // BLOB OPERATIONS (BLOCK + DIRECT)
    // ================================

    // PUT /{account}/{container}/{blobName}
    // PUT /{account}/{container}/{blobName}?comp=block&blockid=...
    // PUT /{account}/{container}/{blobName}?comp=blocklist
    [HttpPut("{*blobName}")]
    public async Task<IActionResult> PutBlob(
        string account,
        string container,
        string blobName,
        [FromQuery] string? comp,
        [FromQuery(Name = "blockid")] string? blockId)
    {
        // 1) PUT block
        if (string.Equals(comp, "block", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(blockId))
                return BadRequest("Missing block id");

            // Azure gửi blockid = base64; convert về string để dùng làm file name
            var blockIdString = Encoding.UTF8.GetString(Convert.FromBase64String(blockId));

            _logger.LogInformation("PUT block: {A}/{C}/{B}, BlockId={Block}",
                account, container, blobName, blockIdString);

            await _backend.SaveBlockAsync(account, container, blobName, blockIdString, Request.Body);
            return Created(string.Empty, null);
        }

        // 2) PUT blocklist
        if (string.Equals(comp, "blocklist", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(Request.Body);
            var xmlText = await reader.ReadToEndAsync();

            var xml = XElement.Parse(xmlText);
            var blockIds = xml.Elements("Latest")
                              .Select(x => x.Value)
                              .ToList();

            _logger.LogInformation("PUT blocklist: {A}/{C}/{B}, Count={Count}",
                account, container, blobName, blockIds.Count);

            await _backend.CommitBlocksAsync(account, container, blobName, blockIds);
            return Created($"/{account}/{container}/{blobName}", null);
        }

        // 3) PUT blob trực tiếp (không block)
        _logger.LogInformation("PUT blob (single-shot): {A}/{C}/{B}",
            account, container, blobName);

        // dùng 1 block giả, rồi commit như blocklist
        const string singleBlockId = "_singleblock";

        await _backend.SaveBlockAsync(account, container, blobName, singleBlockId, Request.Body);
        await _backend.CommitBlocksAsync(account, container, blobName, new List<string> { singleBlockId });

        return Created($"/{account}/{container}/{blobName}", null);
    }

    // GET /{account}/{container}/{blobName}
    [HttpGet("{*blobName}")]
    public IActionResult GetBlob(
        string account,
        string container,
        string blobName)
    {
        var stream = _backend.GetBlob(account, container, blobName);
        if (stream == null)
            return NotFound();

        return File(stream, "application/octet-stream", enableRangeProcessing: true);
    }

    // DELETE /{account}/{container}/{blobName}
    [HttpDelete("{*blobName}")]
    public IActionResult DeleteBlob(
        string account,
        string container,
        string blobName)
    {
        var ok = _backend.DeleteBlob(account, container, blobName);
        return ok ? Accepted() : NotFound();
    }
}
