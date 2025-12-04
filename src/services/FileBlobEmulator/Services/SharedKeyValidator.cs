using System.Security.Cryptography;
using System.Text;

namespace FileBlobEmulator.Services;

public class SharedKeyValidator
{
    private readonly string _account;
    private readonly byte[] _keyBytes;

    public SharedKeyValidator(string account, string keyBase64)
    {
        _account = account;
        _keyBytes = Convert.FromBase64String(keyBase64);
    }

    public bool Validate(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var authHeader))
            return false;

        var auth = authHeader.ToString();
        if (!auth.StartsWith("SharedKey ", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = auth.Substring("SharedKey ".Length).Split(':');
        if (parts.Length != 2)
            return false;

        var account = parts[0];
        var signature = parts[1];

        if (!string.Equals(account, _account, StringComparison.OrdinalIgnoreCase))
            return false;

        var canonicalString = BuildCanonicalString(request);

        using var hmac = new HMACSHA256(_keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));
        var expected = Convert.ToBase64String(hash);

        return expected == signature;
    }

    private string BuildCanonicalString(HttpRequest req)
    {
        var headers = req.Headers;
        var sb = new StringBuilder();

        string Header(string name)
        {
            return headers.ContainsKey(name) ? headers[name].ToString() : "";
        }

        // Azure canonical format
        sb.Append(req.Method).Append("\n");
        sb.Append(Header("Content-Encoding")).Append("\n");
        sb.Append(Header("Content-Language")).Append("\n");

        // Special rule: if Content-Length is 0 => empty string
        var cl = Header("Content-Length");
        sb.Append(cl == "0" ? "" : cl).Append("\n");

        sb.Append(Header("Content-MD5")).Append("\n");
        sb.Append(Header("Content-Type")).Append("\n");
        sb.Append("\n"); // Date must always be empty when using x-ms-date
        sb.Append(Header("If-Modified-Since")).Append("\n");
        sb.Append(Header("If-Match")).Append("\n");
        sb.Append(Header("If-None-Match")).Append("\n");
        sb.Append(Header("If-Unmodified-Since")).Append("\n");
        sb.Append(Header("Range")).Append("\n");

        // Canonicalized x-ms- headers
        foreach (var h in headers
                     .Where(h => h.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(h => h.Key.ToLowerInvariant()))
        {
            sb.Append(h.Key.ToLowerInvariant())
                .Append(':')
                .Append(string.Join(",", h.Value.ToArray()).Trim())
                .Append("\n");
        }


        // Canonicalized resource
        sb.Append("/").Append(_account).Append(req.Path);

        // Canonicalized query params
        var query = req.Query.OrderBy(q => q.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in query)
        {
            sb.Append("\n")
                .Append(kv.Key.ToLowerInvariant())
                .Append(':')
                .Append(string.Join(",", kv.Value.ToArray()));
        }


        return sb.ToString();
    }
}