using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Radio Browser API client. It only reads the documented station catalog and
/// reports a user-initiated station start through the click counter. Stream
/// audio goes directly from the station to the playback engine; this client
/// never downloads, records, proxies, or persists station audio.
/// </summary>
public sealed class RadioBrowserService : IRadioStationService, IDisposable
{
    private const int ResultLimit = 60;
    private const int MaximumSearchCharacters = 100;
    private const int MaximumResponseBytes = 2 * 1024 * 1024;
    private const string MirrorDiscoveryHost = "all.api.radio-browser.info";

    private static readonly Uri[] FallbackMirrors =
    [
        new("https://de1.api.radio-browser.info/"),
        new("https://de2.api.radio-browser.info/"),
        new("https://nl1.api.radio-browser.info/")
    ];

    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly Func<CancellationToken, Task<IReadOnlyList<Uri>>> mirrorResolver;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly ProductInfoHeaderValue userAgent;
    private readonly SemaphoreSlim mirrorGate = new(1, 1);
    private IReadOnlyList<Uri>? mirrors;
    private bool disposed;

    public RadioBrowserService()
        : this(
            CreateDefaultHttpClient(),
            mirrorResolver: null,
            delayAsync: null,
            GetApplicationVersion(),
            ownsHttpClient: true)
    {
    }

    internal RadioBrowserService(
        HttpClient httpClient,
        Func<CancellationToken, Task<IReadOnlyList<Uri>>>? mirrorResolver = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        string applicationVersion = "0.6.0")
        : this(
            httpClient,
            mirrorResolver,
            delayAsync,
            applicationVersion,
            ownsHttpClient: false)
    {
    }

    private RadioBrowserService(
        HttpClient httpClient,
        Func<CancellationToken, Task<IReadOnlyList<Uri>>>? mirrorResolver,
        Func<TimeSpan, CancellationToken, Task>? delayAsync,
        string applicationVersion,
        bool ownsHttpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.mirrorResolver = mirrorResolver ?? ResolveMirrorsAsync;
        this.delayAsync = delayAsync ?? Task.Delay;
        this.ownsHttpClient = ownsHttpClient;
        userAgent = new ProductInfoHeaderValue(
            "Mimi",
            string.IsNullOrWhiteSpace(applicationVersion)
                ? "0.6.0"
                : applicationVersion.Trim());
    }

    public async Task<IReadOnlyList<RadioStation>> GetTopStationsAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await SendAsync(
            $"json/stations/topvote/{ResultLimit}?hidebroken=true",
            allowMirrorFailover: true,
            cancellationToken).ConfigureAwait(false);
        return ParseStations(json);
    }

    public async Task<IReadOnlyList<RadioStation>> SearchStationsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
            return await GetTopStationsAsync(cancellationToken).ConfigureAwait(false);

        if (normalizedQuery.Length > MaximumSearchCharacters)
            normalizedQuery = normalizedQuery[..MaximumSearchCharacters];

        var relativePath =
            "json/stations/search?" +
            $"name={Uri.EscapeDataString(normalizedQuery)}" +
            $"&limit={ResultLimit}&hidebroken=true&order=votes&reverse=true";
        var json = await SendAsync(
            relativePath,
            allowMirrorFailover: true,
            cancellationToken).ConfigureAwait(false);
        return ParseStations(json);
    }

    public async Task RegisterStationClickAsync(
        Guid stationUuid,
        CancellationToken cancellationToken = default)
    {
        if (stationUuid == Guid.Empty)
            throw new ArgumentException("A station UUID is required.", nameof(stationUuid));

        // A click count is not idempotent. Use one request to one mirror so
        // a network retry can never silently count the same user action
        // more than once. The caller treats this as best effort only.
        _ = await SendAsync(
            $"json/url/{stationUuid:D}",
            allowMirrorFailover: false,
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        if (ownsHttpClient)
            httpClient.Dispose();
        mirrorGate.Dispose();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    private static string GetApplicationVersion()
    {
        var version =
            Assembly.GetEntryAssembly()?.GetName().Version ??
            typeof(RadioBrowserService).Assembly.GetName().Version;
        return version?.ToString(3) ?? "0.6.0";
    }

    private async Task<string> SendAsync(
        string relativePath,
        bool allowMirrorFailover,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var candidates = await GetMirrorsAsync(cancellationToken).ConfigureAwait(false);
        var attemptLimit = allowMirrorFailover
            ? Math.Min(candidates.Count, 3)
            : Math.Min(candidates.Count, 1);
        Exception? lastFailure = null;

        for (var attempt = 0; attempt < attemptLimit; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mirror = candidates[attempt];
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(mirror, relativePath));
                request.Headers.UserAgent.Add(userAgent);
                request.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await ReadBoundedContentAsync(
                        response,
                        cancellationToken).ConfigureAwait(false);
                    RememberHealthyMirror(mirror);
                    return content;
                }

                if (!IsTransientStatus(response.StatusCode))
                {
                    throw new RadioServiceException(
                        $"Radio Browser returned HTTP {(int)response.StatusCode}.");
                }

                lastFailure = new HttpRequestException(
                    $"Radio Browser returned HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (RadioServiceException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or IOException or OperationCanceledException)
            {
                lastFailure = ex;
            }

            if (attempt + 1 < attemptLimit)
            {
                var delay = TimeSpan.FromMilliseconds(attempt == 0 ? 250 : 750);
                await delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new RadioServiceException(
            "Radio Browser is currently unavailable.",
            lastFailure ?? new HttpRequestException("No Radio Browser mirror was available."));
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static async Task<string> ReadBoundedContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            throw new RadioServiceException("Radio Browser returned an unexpectedly large response.");

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (memory.Length + read > MaximumResponseBytes)
            {
                throw new RadioServiceException(
                    "Radio Browser returned an unexpectedly large response.");
            }
            memory.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private async Task<IReadOnlyList<Uri>> GetMirrorsAsync(CancellationToken cancellationToken)
    {
        if (mirrors is { Count: > 0 } cached)
            return cached;

        await mirrorGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (mirrors is { Count: > 0 } resolved)
                return resolved;

            try
            {
                var discovered = await mirrorResolver(cancellationToken).ConfigureAwait(false);
                mirrors = NormalizeMirrors(discovered);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                mirrors = FallbackMirrors;
            }

            if (mirrors.Count == 0)
                mirrors = FallbackMirrors;
            return mirrors;
        }
        finally
        {
            mirrorGate.Release();
        }
    }

    private void RememberHealthyMirror(Uri mirror)
    {
        var current = mirrors;
        if (current is null || current.Count == 0 || current[0] == mirror)
            return;

        var reordered = new List<Uri>(current.Count) { mirror };
        foreach (var candidate in current)
        {
            if (candidate != mirror)
                reordered.Add(candidate);
        }
        mirrors = reordered;
    }

    private static async Task<IReadOnlyList<Uri>> ResolveMirrorsAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));

        var addresses = await Dns.GetHostAddressesAsync(
            MirrorDiscoveryHost,
            AddressFamily.Unspecified,
            timeout.Token).ConfigureAwait(false);
        var result = new List<Uri>();

        foreach (var address in addresses.Distinct())
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(
                    address.ToString(),
                    AddressFamily.Unspecified,
                    timeout.Token).ConfigureAwait(false);
                var host = entry.HostName?.Trim().TrimEnd('.');
                if (!string.IsNullOrWhiteSpace(host) &&
                    Uri.TryCreate($"https://{host}/", UriKind.Absolute, out var mirror))
                {
                    result.Add(mirror);
                }
            }
            catch (SocketException)
            {
                // One unresolvable address should not discard other mirrors.
            }

            if (result.Count >= 5)
                break;
        }

        return NormalizeMirrors(result);
    }

    private static IReadOnlyList<Uri> NormalizeMirrors(IEnumerable<Uri>? candidates)
    {
        if (candidates is null) return [];

        var result = new List<Uri>();
        foreach (var candidate in candidates)
        {
            if (!candidate.IsAbsoluteUri ||
                !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(candidate.UserInfo))
            {
                continue;
            }

            var normalized = new Uri(candidate.AbsoluteUri.TrimEnd('/') + "/");
            if (!result.Contains(normalized))
                result.Add(normalized);
        }
        return result;
    }

    internal static IReadOnlyList<RadioStation> ParseStations(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new RadioServiceException("Radio Browser returned an invalid station list.");

            var stations = new List<RadioStation>(ResultLimit);
            var seen = new HashSet<Guid>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!TryParseStation(element, out var station))
                    continue;
                if (!seen.Add(station.StationUuid))
                    continue;

                stations.Add(station);
                if (stations.Count >= ResultLimit)
                    break;
            }
            return stations;
        }
        catch (JsonException ex)
        {
            throw new RadioServiceException(
                "Radio Browser returned an invalid station list.",
                ex);
        }
    }

    private static bool TryParseStation(JsonElement element, out RadioStation station)
    {
        station = null!;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        var uuidText = GetString(element, "stationuuid");
        if (!Guid.TryParse(uuidText, out var stationUuid) || stationUuid == Guid.Empty)
            return false;

        var name = CleanText(GetString(element, "name"), maximumLength: 200);
        if (name.Length == 0)
            return false;

        if (GetInt(element, "lastcheckok") != 1 || GetInt(element, "hls") == 1)
            return false;

        if (!TryCreateWebUri(GetString(element, "url_resolved"), out var streamUrl) ||
            streamUrl is null)
        {
            return false;
        }

        TryCreateWebUri(GetString(element, "homepage"), out var homepageUrl);
        TryCreateWebUri(GetString(element, "favicon"), out var faviconUrl);

        station = new RadioStation(
            stationUuid,
            name,
            streamUrl,
            homepageUrl,
            faviconUrl,
            CleanText(GetString(element, "country"), maximumLength: 100),
            CleanText(GetString(element, "language"), maximumLength: 100),
            ParseTags(GetString(element, "tags")),
            CleanText(GetString(element, "codec"), maximumLength: 40),
            Math.Max(0, GetInt(element, "bitrate")),
            Math.Max(0, GetInt(element, "votes")),
            Math.Max(0, GetInt(element, "clickcount")));
        return true;
    }

    private static IReadOnlyList<string> ParseTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value.Split(',')
            .Select(tag => CleanText(tag, maximumLength: 40))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return 0;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static bool TryCreateWebUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if ((candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(candidate.UserInfo))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static string CleanText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var cleaned = string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength].TrimEnd();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
