using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileBlobEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileBlobEmulator.Controllers;

[ApiController]
[Route("{account}/{container}")]
public partial class BlobController : ControllerBase
{
    private readonly BlobFileBackend _backend;
    private readonly ILogger<BlobController> _logger;

    // Maximum lengths for path segments
    private const int MaxAccountLength = 64;
    private const int MaxContainerLength = 63;
    private const int MaxBlobNameLength = 1024;
    private const int MaxBlockIdLength = 64;

    public BlobController(BlobFileBackend backend, ILogger<BlobController> logger)
    {
        _backend = backend;
        _logger = logger;
    }

    // ================================
    // INPUT VALIDATION
    // ================================

    /// <summary>
    /// Validates account name - must be alphanumeric lowercase, 3-64 chars
    /// </summary>
    private static bool IsValidAccountName(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return false;
        if (account.Length < 3 || account.Length > MaxAccountLength) return false;
        return AccountNameRegex().IsMatch(account);
    }

    /// <summary>
    /// Validates container name - alphanumeric lowercase + dash, 3-63 chars
    /// </summary>
    private static bool IsValidContainerName(string? container)
    {
        if (string.IsNullOrWhiteSpace(container)) return false;
        if (container.Length < 3 || container.Length > MaxContainerLength) return false;
        // Cannot start/end with dash, no consecutive dashes
        if (container.StartsWith('-') || container.EndsWith('-')) return false;
        if (container.Contains("--")) return false;
        return ContainerNameRegex().IsMatch(container);
    }

    /// <summary>
    /// Validates blob name - no path traversal, reasonable length
    /// </summary>
    private static bool IsValidBlobName(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName)) return false;
        if (blobName.Length > MaxBlobNameLength) return false;
        // Check for path traversal attempts
        if (blobName.Contains("..")) return false;
        if (blobName.StartsWith('/') || blobName.StartsWith('\\')) return false;
        // Check for invalid characters
        return BlobNameRegex().IsMatch(blobName);
    }

    /// <summary>
    /// Validates block ID after base64 decode
    /// </summary>
    private static bool IsValidBlockId(string? blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId)) return false;
        if (blockId.Length > MaxBlockIdLength) return false;
        return BlockIdRegex().IsMatch(blockId);
    }

    /// <summary>
    /// Sanitize string for safe XML output - prevents XSS attacks
    /// </summary>
    private static string SanitizeForXmlOutput(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Use SecurityElement.Escape for proper XML escaping
        // This escapes: < > & " '
        return System.Security.SecurityElement.Escape(input) ?? string.Empty;
    }

    /// <summary>
    /// Sanitize string for safe logging - prevents Log Forging/Injection attacks
    /// Removes newlines, carriage returns, and other control characters
    /// </summary>
    private static string SanitizeForLog(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Remove newlines, carriage returns, tabs, and other control characters
        // that could be used to forge log entries
        return LogSanitizeRegex().Replace(input, "_");
    }

    /// <summary>
    /// Validates all path parameters and returns BadRequest if invalid
    /// </summary>
    private IActionResult? ValidatePathParameters(string account, string container, string? blobName = null)
    {
        if (!IsValidAccountName(account))
            return BadRequest(new
            {
                error = "Invalid account name", details = "Account name must be 3-64 lowercase alphanumeric characters"
            });

        if (!IsValidContainerName(container))
            return BadRequest(new
            {
                error = "Invalid container name",
                details = "Container name must be 3-63 lowercase alphanumeric characters or dashes"
            });

        if (blobName != null && !IsValidBlobName(blobName))
            return BadRequest(new
            {
                error = "Invalid blob name",
                details = "Blob name contains invalid characters or path traversal attempts"
            });

        return null; // Valid
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

        var validationError = ValidatePathParameters(account, container);
        if (validationError != null) return validationError;

        _logger.LogInformation("Create container {Account}/{Container}", SanitizeForLog(account),
            SanitizeForLog(container));

        try
        {
            _backend.EnsureContainer(account, container);
            return Created($"/{account}/{container}", null);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Invalid path", details = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Path traversal attempt: {Message}", SanitizeForLog(ex.Message));
            return StatusCode(403, new { error = "Access denied" });
        }
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

        var validationError = ValidatePathParameters(account, container);
        if (validationError != null) return validationError;

        _logger.LogInformation("Delete container {Account}/{Container}", SanitizeForLog(account),
            SanitizeForLog(container));

        try
        {
            _backend.DeleteContainer(account, container);
            return Accepted();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Invalid path", details = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Path traversal attempt: {Message}", SanitizeForLog(ex.Message));
            return StatusCode(403, new { error = "Access denied" });
        }
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

        var validationError = ValidatePathParameters(account, container);
        if (validationError != null) return validationError;

        try
        {
            var blobs = _backend.ListBlobs(account, container).ToList();

            // Sanitize blob names to prevent XSS - only allow safe characters in output
            var sanitizedBlobs = blobs.Select(b => SanitizeForXmlOutput(b)).ToList();

            var xml = new XElement("EnumerationResults",
                new XAttribute("ContainerName", $"{SanitizeForXmlOutput(account)}/{SanitizeForXmlOutput(container)}"),
                new XElement("Blobs",
                    sanitizedBlobs.Select(b => new XElement("Blob",
                        new XElement("Name", b)
                    ))
                ),
                new XElement("NextMarker", "")
            );

            // Set security headers to prevent content sniffing
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            return Content(xml.ToString(), "application/xml; charset=utf-8");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Invalid path", details = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Path traversal attempt: {Message}", SanitizeForLog(ex.Message));
            return StatusCode(403, new { error = "Access denied" });
        }
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
        var validationError = ValidatePathParameters(account, container, blobName);
        if (validationError != null) return validationError;

        try
        {
            // 1) PUT block
            if (string.Equals(comp, "block", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(blockId))
                    return BadRequest("Missing block id");

                // Azure sends blockid as base64; decode to string for file name
                string blockIdString;
                try
                {
                    blockIdString = Encoding.UTF8.GetString(Convert.FromBase64String(blockId));
                }
                catch (FormatException)
                {
                    return BadRequest(new { error = "Invalid block id", details = "Block ID must be valid base64" });
                }

                if (!IsValidBlockId(blockIdString))
                    return BadRequest(new
                        { error = "Invalid block id", details = "Block ID contains invalid characters" });

                _logger.LogInformation("PUT block: {A}/{C}/{B}, BlockId={Block}",
                    SanitizeForLog(account), SanitizeForLog(container), SanitizeForLog(blobName),
                    SanitizeForLog(blockIdString));

                await _backend.SaveBlockAsync(account, container, blobName, blockIdString, Request.Body);
                return Created(string.Empty, null);
            }

            // 2) PUT blocklist
            if (string.Equals(comp, "blocklist", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(Request.Body);
                var xmlText = await reader.ReadToEndAsync();

                XElement xml;
                try
                {
                    xml = XElement.Parse(xmlText);
                }
                catch (Exception)
                {
                    return BadRequest(new { error = "Invalid XML", details = "Block list must be valid XML" });
                }

                var blockIds = xml.Elements("Latest")
                    .Select(x => x.Value)
                    .ToList();

                // Validate all block IDs
                foreach (var id in blockIds)
                {
                    if (!IsValidBlockId(id))
                        return BadRequest(new
                        {
                            error = "Invalid block id in list",
                            details = $"Block ID '{SanitizeForLog(id)}' contains invalid characters"
                        });
                }

                _logger.LogInformation("PUT blocklist: {A}/{C}/{B}, Count={Count}",
                    SanitizeForLog(account), SanitizeForLog(container), SanitizeForLog(blobName), blockIds.Count);

                await _backend.CommitBlocksAsync(account, container, blobName, blockIds);
                return Created($"/{account}/{container}/{blobName}", null);
            }

            // 3) PUT blob directly (single-shot)
            _logger.LogInformation("PUT blob (single-shot): {A}/{C}/{B}",
                SanitizeForLog(account), SanitizeForLog(container), SanitizeForLog(blobName));

            const string singleBlockId = "_singleblock";

            await _backend.SaveBlockAsync(account, container, blobName, singleBlockId, Request.Body);
            await _backend.CommitBlocksAsync(account, container, blobName, new List<string> { singleBlockId });

            return Created($"/{account}/{container}/{blobName}", null);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Invalid path", details = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Path traversal attempt: {Message}", SanitizeForLog(ex.Message));
            return StatusCode(403, new { error = "Access denied" });
        }
    }

    // GET /{account}/{container}/{blobName}
    [HttpGet("{*blobName}")]
    public IActionResult GetBlob(
        string account,
        string container,
        string blobName)
    {
        var validationError = ValidatePathParameters(account, container, blobName);
        if (validationError != null) return validationError;

        try
        {
            var stream = _backend.GetBlob(account, container, blobName);
            if (stream == null)
                return NotFound();

            return File(stream, "application/octet-stream", enableRangeProcessing: true);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Invalid path", details = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Path traversal attempt: {Message}", SanitizeForLog(ex.Message));
            return StatusCode(403, new { error = "Access denied" });
        }
    }

    // DELETE /{account}/{container}/{blobName}
    [HttpDelete("{*blobName}")]
    public IActionResult DeleteBlob(
        string account,
        string container,
        string blobName)
    {
        var validationError = ValidatePathParameters(account, container, blobName);
        if (validationError != null) return validationError;

        try
        {
            var ok = _backend.DeleteBlob(account, container, blobName);
            return ok ? Accepted() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "Invalid path", details = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Path traversal attempt: {Message}", SanitizeForLog(ex.Message));
            return StatusCode(403, new { error = "Access denied" });
        }
    }

    // ================================
    // REGEX PATTERNS (compiled)
    // ================================

    [GeneratedRegex(@"^[a-z0-9]+$")]
    private static partial Regex AccountNameRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]{3}$")]
    private static partial Regex ContainerNameRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9\-_\.\/]+$")]
    private static partial Regex BlobNameRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex BlockIdRegex();

    // Matches newlines, carriage returns, tabs, and other control characters
    [GeneratedRegex(@"[\r\n\t\x00-\x1F\x7F]")]
    private static partial Regex LogSanitizeRegex();
}
