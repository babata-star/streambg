using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StreamBG.Infrastructure.Services;

// ── Configuration ─────────────────────────────────────────────────────────────

public class CdnOptions
{
    public const string Section = "Cdn";

    public bool Enabled { get; set; }
    /// <summary>None | Cloudflare | CloudFront</summary>
    public string Provider { get; set; } = "None";

    // CDN base URLs (set these to your CDN distribution URLs)
    public string? HlsBaseUrl { get; set; }
    public string? VodBaseUrl { get; set; }
    public string? ThumbnailBaseUrl { get; set; }

    public CloudflareOptions Cloudflare { get; set; } = new();
    public CloudFrontOptions CloudFront { get; set; } = new();
}

public class CloudflareOptions
{
    /// <summary>Cloudflare Zone ID (from the Overview page)</summary>
    public string? ZoneId { get; set; }
    /// <summary>Cloudflare API Token with Cache Purge permission</summary>
    public string? ApiToken { get; set; }
}

public class CloudFrontOptions
{
    public string? DistributionId { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string Region { get; set; } = "us-east-1";

    // Signed URL support (for private/premium streams)
    public bool UseSignedUrls { get; set; }
    public string? KeyPairId { get; set; }
    /// <summary>RSA private key in PEM format</summary>
    public string? PrivateKeyPem { get; set; }
    public int SignedUrlExpiryMinutes { get; set; } = 60;
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface ICdnService
{
    bool IsEnabled { get; }

    /// <summary>Live HLS playlist URL (signed if CloudFront + UseSignedUrls).</summary>
    string HlsUrl(string streamKey);

    /// <summary>VOD master playlist URL (signed if CloudFront + UseSignedUrls).</summary>
    string VodUrl(string userId, int vodId);

    /// <summary>Thumbnail URL. Falls back to fallback if path is empty.</summary>
    string ThumbnailUrl(string? path, string? fallback = null);

    /// <summary>MP4 URL for a short video clip.</summary>
    string ClipUrl(int clipId);

    /// <summary>JPEG thumbnail URL for a clip.</summary>
    string ClipThumbnailUrl(int clipId);

    /// <summary>Purge CDN cache for the given path patterns (e.g. "/hls/key/*").</summary>
    Task InvalidateAsync(params string[] patterns);
}

// ── Implementation ────────────────────────────────────────────────────────────

public class CdnService : ICdnService
{
    private readonly CdnOptions _opt;
    private readonly string _hlsOrigin;
    private readonly string _vodOrigin;
    private readonly string _mediaBase;   // scheme+host only, e.g. http://nginx-rtmp:8080
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CdnService> _log;

    public bool IsEnabled => _opt.Enabled;

    public CdnService(
        IOptions<CdnOptions> opt,
        IHttpClientFactory http,
        ILogger<CdnService> log,
        string hlsOrigin,
        string vodOrigin)
    {
        _opt = opt.Value;
        _http = http;
        _log = log;
        _hlsOrigin = hlsOrigin;
        _vodOrigin = vodOrigin;
        // Strip the /hls path suffix to get the base media server URL
        _mediaBase = hlsOrigin.TrimEnd('/').EndsWith("/hls", StringComparison.OrdinalIgnoreCase)
            ? hlsOrigin.TrimEnd('/')[..^4]
            : hlsOrigin.TrimEnd('/');
    }

    // ── URL generation ────────────────────────────────────────────────────────

    public string HlsUrl(string streamKey)
    {
        var baseUrl = IsEnabled && !string.IsNullOrEmpty(_opt.HlsBaseUrl)
            ? _opt.HlsBaseUrl : _hlsOrigin;

        // Points to the ABR master playlist; HLS players pick the right quality automatically.
        var url = $"{baseUrl.TrimEnd('/')}/{streamKey}/master.m3u8";

        return _opt.CloudFront.UseSignedUrls
            ? SignCloudFrontUrl(url)
            : url;
    }

    public string VodUrl(string userId, int vodId)
    {
        var baseUrl = IsEnabled && !string.IsNullOrEmpty(_opt.VodBaseUrl)
            ? _opt.VodBaseUrl : _vodOrigin;

        var url = $"{baseUrl.TrimEnd('/')}/{userId}/{vodId}/master.m3u8";

        return _opt.CloudFront.UseSignedUrls
            ? SignCloudFrontUrl(url)
            : url;
    }

    public string ThumbnailUrl(string? path, string? fallback = null)
    {
        if (string.IsNullOrEmpty(path)) return fallback ?? string.Empty;
        if (Uri.IsWellFormedUriString(path, UriKind.Absolute)) return path;

        var baseUrl = IsEnabled && !string.IsNullOrEmpty(_opt.ThumbnailBaseUrl)
            ? _opt.ThumbnailBaseUrl
            : _hlsOrigin.Replace("/hls", "/thumbnails");

        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    public string ClipUrl(int clipId)
    {
        var base_ = IsEnabled && !string.IsNullOrEmpty(_opt.HlsBaseUrl)
            ? _opt.HlsBaseUrl.TrimEnd('/').EndsWith("/hls", StringComparison.OrdinalIgnoreCase)
                ? _opt.HlsBaseUrl.TrimEnd('/')[..^4]
                : _opt.HlsBaseUrl.TrimEnd('/')
            : _mediaBase;

        var url = $"{base_}/clips/{clipId}.mp4";
        return _opt.CloudFront.UseSignedUrls ? SignCloudFrontUrl(url) : url;
    }

    public string ClipThumbnailUrl(int clipId)
    {
        var base_ = IsEnabled && !string.IsNullOrEmpty(_opt.HlsBaseUrl)
            ? _opt.HlsBaseUrl.TrimEnd('/').EndsWith("/hls", StringComparison.OrdinalIgnoreCase)
                ? _opt.HlsBaseUrl.TrimEnd('/')[..^4]
                : _opt.HlsBaseUrl.TrimEnd('/')
            : _mediaBase;

        return $"{base_}/clips/{clipId}.jpg";
    }

    // ── Cache invalidation ────────────────────────────────────────────────────

    public async Task InvalidateAsync(params string[] patterns)
    {
        if (!IsEnabled || patterns.Length == 0) return;
        try
        {
            if (_opt.Provider == "Cloudflare")
                await CloudflareInvalidateAsync(patterns);
            else if (_opt.Provider == "CloudFront")
                await CloudFrontInvalidateAsync(patterns);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "CDN invalidation failed for {Patterns}", string.Join(", ", patterns));
        }
    }

    // ── Cloudflare ────────────────────────────────────────────────────────────

    private async Task CloudflareInvalidateAsync(string[] patterns)
    {
        var cf = _opt.Cloudflare;
        if (string.IsNullOrEmpty(cf.ZoneId) || string.IsNullOrEmpty(cf.ApiToken))
        {
            _log.LogWarning("Cloudflare ZoneId or ApiToken not configured");
            return;
        }

        // Expand pattern paths to absolute CDN URLs
        var cdnRoot = (_opt.HlsBaseUrl ?? _hlsOrigin).Split("/hls")[0];
        var files = patterns.Select(p => $"{cdnRoot}{p}").ToArray();

        using var client = _http.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cf.ApiToken}");

        var json = JsonSerializer.Serialize(new { files });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await client.PostAsync(
            $"https://api.cloudflare.com/client/v4/zones/{cf.ZoneId}/purge_cache",
            content);

        if (resp.IsSuccessStatusCode)
            _log.LogInformation("Cloudflare: purged {N} paths", files.Length);
        else
        {
            var body = await resp.Content.ReadAsStringAsync();
            _log.LogWarning("Cloudflare purge failed ({Status}): {Body}", resp.StatusCode, body);
        }
    }

    // ── CloudFront invalidation (SigV4) ───────────────────────────────────────

    private async Task CloudFrontInvalidateAsync(string[] patterns)
    {
        var cf = _opt.CloudFront;
        if (string.IsNullOrEmpty(cf.DistributionId) ||
            string.IsNullOrEmpty(cf.AccessKeyId)    ||
            string.IsNullOrEmpty(cf.SecretAccessKey))
        {
            _log.LogWarning("CloudFront credentials not configured — skipping invalidation");
            return;
        }

        var pathItems = string.Join("", patterns.Select(p => $"<Path>{p}</Path>"));
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <InvalidationBatch>
              <Paths>
                <Quantity>{patterns.Length}</Quantity>
                <Items>{pathItems}</Items>
              </Paths>
              <CallerReference>{DateTime.UtcNow.Ticks}</CallerReference>
            </InvalidationBatch>
            """;

        var host       = "cloudfront.amazonaws.com";
        var path       = $"/2020-11-01/distribution/{cf.DistributionId}/invalidation";
        var endpoint   = $"https://{host}{path}";
        var now        = DateTime.UtcNow;
        var amzDate    = now.ToString("yyyyMMddTHHmmssZ");
        var dateStamp  = now.ToString("yyyyMMdd");
        var bodyHash   = Sha256Hex(xml);
        var contentType = "application/xml";

        // Canonical request
        var canonicalRequest =
            $"POST\n{path}\n\n" +
            $"content-type:{contentType}\nhost:{host}\nx-amz-date:{amzDate}\n\n" +
            $"content-type;host;x-amz-date\n{bodyHash}";

        var credScope    = $"{dateStamp}/{cf.Region}/cloudfront/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credScope}\n{Sha256Hex(canonicalRequest)}";
        var signingKey   = DeriveSigningKey(cf.SecretAccessKey, dateStamp, cf.Region, "cloudfront");
        var signature    = HmacSha256Hex(signingKey, stringToSign);

        var authorization =
            $"AWS4-HMAC-SHA256 Credential={cf.AccessKeyId}/{credScope}, " +
            $"SignedHeaders=content-type;host;x-amz-date, Signature={signature}";

        using var client = _http.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Add("x-amz-date", amzDate);
        req.Headers.Add("Authorization", authorization);
        req.Content = new StringContent(xml, Encoding.UTF8, contentType);

        var resp = await client.SendAsync(req);
        if (resp.IsSuccessStatusCode)
            _log.LogInformation("CloudFront: invalidation queued for {DistId}", cf.DistributionId);
        else
        {
            var body = await resp.Content.ReadAsStringAsync();
            _log.LogWarning("CloudFront invalidation failed ({Status}): {Body}", resp.StatusCode, body);
        }
    }

    // ── CloudFront signed URLs (RSA-SHA1, canned policy) ─────────────────────

    private string SignCloudFrontUrl(string url)
    {
        var cf = _opt.CloudFront;
        if (string.IsNullOrEmpty(cf.PrivateKeyPem) || string.IsNullOrEmpty(cf.KeyPairId))
            return url;

        var expires = DateTimeOffset.UtcNow.AddMinutes(cf.SignedUrlExpiryMinutes).ToUnixTimeSeconds();

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(cf.PrivateKeyPem);

            var policy = $"{{\"Statement\":[{{\"Resource\":\"{url}\"," +
                         $"\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{expires}}}}}}}]}}";

            var policyBytes = Encoding.UTF8.GetBytes(policy);
            var signature   = rsa.SignData(policyBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);

            // CloudFront uses URL-safe base64: + → - / → ~ = → _
            string CfBase64(byte[] b) => Convert.ToBase64String(b)
                .Replace('+', '-').Replace('/', '~').Replace('=', '_');

            return $"{url}?Policy={CfBase64(policyBytes)}" +
                   $"&Signature={CfBase64(signature)}" +
                   $"&Key-Pair-Id={cf.KeyPairId}";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "CloudFront URL signing failed — returning unsigned URL");
            return url;
        }
    }

    // ── SigV4 helpers ─────────────────────────────────────────────────────────

    private static string Sha256Hex(string data)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private static string HmacSha256Hex(byte[] key, string data)
        => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kDate    = HMACSHA256.HashData(Encoding.UTF8.GetBytes($"AWS4{secretKey}"), Encoding.UTF8.GetBytes(dateStamp));
        var kRegion  = HMACSHA256.HashData(kDate,    Encoding.UTF8.GetBytes(region));
        var kService = HMACSHA256.HashData(kRegion,  Encoding.UTF8.GetBytes(service));
        return   HMACSHA256.HashData(kService, "aws4_request"u8.ToArray());
    }
}

// ── No-op fallback (CDN disabled) ────────────────────────────────────────────

public class NullCdnService : ICdnService
{
    private readonly string _hlsBase;
    private readonly string _vodBase;

    public bool IsEnabled => false;

    public NullCdnService(string hlsBase, string vodBase)
    {
        _hlsBase = hlsBase;
        _vodBase = vodBase;
    }

    public string HlsUrl(string streamKey)
        => $"{_hlsBase.TrimEnd('/')}/{streamKey}/master.m3u8";

    public string VodUrl(string userId, int vodId)
        => $"{_vodBase.TrimEnd('/')}/{userId}/{vodId}/master.m3u8";

    public string ClipUrl(int clipId)
    {
        var mediaBase = _hlsBase.TrimEnd('/').EndsWith("/hls", StringComparison.OrdinalIgnoreCase)
            ? _hlsBase.TrimEnd('/')[..^4]
            : _hlsBase.TrimEnd('/');
        return $"{mediaBase}/clips/{clipId}.mp4";
    }

    public string ClipThumbnailUrl(int clipId)
    {
        var mediaBase = _hlsBase.TrimEnd('/').EndsWith("/hls", StringComparison.OrdinalIgnoreCase)
            ? _hlsBase.TrimEnd('/')[..^4]
            : _hlsBase.TrimEnd('/');
        return $"{mediaBase}/clips/{clipId}.jpg";
    }

    public string ThumbnailUrl(string? path, string? fallback = null)
        => Uri.IsWellFormedUriString(path, UriKind.Absolute) ? path! : fallback ?? string.Empty;

    public Task InvalidateAsync(params string[] patterns) => Task.CompletedTask;
}
