using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Versioner.Models;
using Versioner.Options;

namespace Versioner.Services.Parse;

public sealed class AppReleaseClient : IAppReleaseClient, IDisposable
{
    private const string ClassPath = "/classes/AppRelease";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Back4AppOptions _options;
    private readonly ILogger<AppReleaseClient> _logger;

    public AppReleaseClient(IOptions<Back4AppOptions> options, ILogger<AppReleaseClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<AppReleaseRecord>> ListAsync(
        Action<string>? report = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);

        var watch = Stopwatch.StartNew();
        var body = await SendAsync(HttpMethod.Get, ClassPath + "?limit=200&order=-createdAt", null, cancellationToken);
        watch.Stop();
        report?.Invoke($"收到响应 {FormatSize(Encoding.UTF8.GetByteCount(body))}，{FormatElapsed(watch.Elapsed)}");

        var parsed = JsonSerializer.Deserialize<ListResponse>(body, JsonOptions) ?? new ListResponse();
        var records = new List<AppReleaseRecord>();
        foreach (var row in parsed.Results)
        {
            if (string.IsNullOrWhiteSpace(row.ObjectId))
                continue;

            records.Add(new AppReleaseRecord
            {
                ObjectId = row.ObjectId,
                Version = row.Version?.Trim() ?? "",
                Lines = ReadLines(row.Lines),
                Notes = row.Notes ?? "",
                DownloadUrl = row.DownloadUrl?.Trim() ?? "",
                Mandatory = row.Mandatory ?? false,
                Enabled = row.Enabled ?? true,
                UpdatedAt = ParseTime(row.UpdatedAt)
            });
        }

        var skipped = parsed.Results.Count - records.Count;
        report?.Invoke(skipped > 0
            ? $"解析完成，{records.Count} 条，跳过 {skipped} 条无效记录"
            : $"解析完成，{records.Count} 条");
        return records;
    }

    public async Task<string> CreateAsync(AppReleaseDraft draft, CancellationToken cancellationToken = default)
    {
        var body = await SendAsync(HttpMethod.Post, ClassPath, ToPayload(draft, includeAcl: true), cancellationToken);
        var created = JsonSerializer.Deserialize<CreateResponse>(body, JsonOptions);
        if (string.IsNullOrWhiteSpace(created?.ObjectId))
            throw new AppReleaseException("创建成功但没有返回 objectId。");

        return created.ObjectId;
    }

    public Task UpdateAsync(string objectId, AppReleaseDraft draft, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ClassPath + "/" + Uri.EscapeDataString(objectId), ToPayload(draft, includeAcl: false), cancellationToken);

    public Task DeleteAsync(string objectId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, ClassPath + "/" + Uri.EscapeDataString(objectId), null, cancellationToken);

    public void Dispose() => _http.Dispose();

    private static WritePayload ToPayload(AppReleaseDraft draft, bool includeAcl) => new()
    {
        Version = draft.Version,
        Notes = draft.Notes,
        Lines = draft.Lines.ToList(),
        DownloadUrl = draft.DownloadUrl,
        Mandatory = draft.Mandatory,
        Enabled = draft.Enabled,
        Acl = includeAcl
            ? new Dictionary<string, Dictionary<string, bool>>
            {
                ["*"] = new Dictionary<string, bool> { ["read"] = true }
            }
            : null
    };

    private async Task<string> SendAsync(HttpMethod method, string relativeUrl, object? payload, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
            throw new AppReleaseException("未配置 Back4App 密钥。");

        var url = _options.ServerUrl.TrimEnd('/') + relativeUrl;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-Parse-Application-Id", _options.ApplicationId.Trim());
        request.Headers.TryAddWithoutValidation("X-Parse-Master-Key", _options.MasterKey.Trim());
        if (payload is not null)
            request.Content = JsonContent.Create(payload, options: JsonOptions);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Back4App 请求失败 {Method} {Path}，状态码 {StatusCode}",
                    method.Method,
                    relativeUrl,
                    (int)response.StatusCode);
                throw new AppReleaseException(TryReadError(body) ?? $"请求失败（{(int)response.StatusCode}）");
            }

            return body;
        }
        catch (AppReleaseException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Back4App 请求异常 {Method} {Path}", method.Method, relativeUrl);
            throw new AppReleaseException("无法连接 Back4App。", ex);
        }
    }

    private static List<string> ReadLines(JsonElement lines)
    {
        if (lines.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<string>();
        foreach (var item in lines.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                result.Add(item.GetString() ?? "");
        }

        return result;
    }

    private static DateTimeOffset? ParseTime(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            ? time
            : null;

    private static string FormatSize(int bytes) =>
        bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024d:0.0} KB";

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalSeconds >= 1
            ? $"耗时 {elapsed.TotalSeconds:0.0} 秒"
            : $"耗时 {elapsed.TotalMilliseconds:0} ms";

    private static string? TryReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString();
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed class ListResponse
    {
        public List<ParseRow> Results { get; set; } = [];
    }

    private sealed class ParseRow
    {
        public string? ObjectId { get; set; }

        public string? Version { get; set; }

        public string? Notes { get; set; }

        public JsonElement Lines { get; set; }

        public string? DownloadUrl { get; set; }

        public bool? Mandatory { get; set; }

        public bool? Enabled { get; set; }

        public string? UpdatedAt { get; set; }
    }

    private sealed class CreateResponse
    {
        public string? ObjectId { get; set; }
    }

    private sealed class WritePayload
    {
        public string Version { get; set; } = "";

        public string Notes { get; set; } = "";

        public List<string> Lines { get; set; } = [];

        public string DownloadUrl { get; set; } = "";

        public bool Mandatory { get; set; }

        public bool Enabled { get; set; }

        [JsonPropertyName("ACL")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Dictionary<string, bool>>? Acl { get; set; }
    }
}
