using System.Net.Http.Headers;
using System.Text.Json;
using Mimi.Online;

namespace Mimi.Services;

/// <summary>
/// Read-only Audius catalog client. It uses the documented public REST API
/// with the descriptive <c>app_name</c> parameter and no credential of any
/// kind, so nothing secret can leak from a distributed build.
///
/// Only streaming is supported: Mimi resolves a provider stream URL for the
/// current session and never downloads, persists, proxies, or re-serves audio.
/// Items whose rights or availability are uncertain are dropped here rather
/// than surfaced and refused later.
/// </summary>
public sealed class AudiusCatalogService : IOnlineCatalogService
{
    public const string ApiBaseUrl = "https://api.audius.co/v1";

    public const string AppName = "Mimi";

    public const string CanonicalSiteUrl = "https://audius.co";

    public const int MaximumResults = 25;

    private const int MaximumResponseBytes = 2_000_000;

    private static readonly string[] ArtworkSizeKeys =
        ["480x480", "150x150", "1000x1000"];

    private readonly HttpClient httpClient;

    private readonly bool ownsHttpClient;

    public AudiusCatalogService(HttpClient? httpClient = null)
    {
        ownsHttpClient = httpClient is null;
        this.httpClient = httpClient ?? CreateDefaultClient();
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Mimi", "0.6.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<IReadOnlyList<OnlineTrack>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return [];

        var requestUri =
            $"{ApiBaseUrl}/tracks/search" +
            $"?query={Uri.EscapeDataString(trimmed)}" +
            $"&limit={MaximumResults}" +
            $"&app_name={Uri.EscapeDataString(AppName)}";

        using var response = await httpClient
            .GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"The catalog answered {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        var payload = await ReadBoundedAsync(response, cancellationToken)
            .ConfigureAwait(false);

        using var document = JsonDocument.Parse(payload);

        return ParseTracks(document.RootElement);
    }

    /// <summary>
    /// Builds the documented stream endpoint. <c>no_redirect=false</c> keeps
    /// the API's own 302 into the content node, which Media Foundation
    /// follows while still honouring HTTP range requests for seeking.
    /// </summary>
    public static Uri BuildStreamUri(string trackId) =>
        new(
            $"{ApiBaseUrl}/tracks/{Uri.EscapeDataString(trackId)}/stream" +
            $"?app_name={Uri.EscapeDataString(AppName)}");

    internal static IReadOnlyList<OnlineTrack> ParseTracks(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var tracks = new List<OnlineTrack>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in data.EnumerateArray())
        {
            if (tracks.Count >= MaximumResults) break;

            var track = TryReadTrack(item);
            if (track is null) continue;
            if (!seen.Add(track.Id)) continue;

            tracks.Add(track);
        }

        return tracks;
    }

    private static OnlineTrack? TryReadTrack(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;

        var id = ReadString(item, "id");
        var title = ReadString(item, "title");
        var artist = ReadArtistName(item);
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }

        // Rights and availability gates. A missing streaming flag is treated
        // as "not proven playable" instead of being optimistically accepted.
        if (!ReadBoolean(item, "is_streamable", fallback: false)) return null;
        if (ReadBoolean(item, "is_stream_gated", fallback: false)) return null;
        if (!ReadBoolean(item, "is_available", fallback: true)) return null;

        if (item.TryGetProperty("allowed_api_keys", out var allowedKeys) &&
            allowedKeys.ValueKind == JsonValueKind.Array &&
            allowedKeys.GetArrayLength() > 0)
        {
            // The creator restricted streaming to specific registered apps.
            return null;
        }

        if (item.TryGetProperty("access", out var access) &&
            access.ValueKind == JsonValueKind.Object &&
            access.TryGetProperty("stream", out var streamAccess) &&
            streamAccess.ValueKind == JsonValueKind.False)
        {
            return null;
        }

        if (!item.TryGetProperty("duration", out var durationElement) ||
            durationElement.ValueKind != JsonValueKind.Number ||
            !durationElement.TryGetInt32(out var durationSeconds) ||
            durationSeconds <= 0)
        {
            return null;
        }

        var permalink = ReadString(item, "permalink");
        if (!TryBuildPermalink(permalink, out var permalinkUri)) return null;

        return new OnlineTrack(
            id.Trim(),
            title.Trim(),
            artist.Trim(),
            TimeSpan.FromSeconds(durationSeconds),
            BuildStreamUri(id.Trim()),
            permalinkUri,
            ReadArtworkUri(item),
            ReadString(item, "license") ?? string.Empty);
    }

    private static string? ReadArtistName(JsonElement item)
    {
        if (item.TryGetProperty("user", out var user) &&
            user.ValueKind == JsonValueKind.Object)
        {
            var name = ReadString(user, "name");
            if (!string.IsNullOrWhiteSpace(name)) return name;

            return ReadString(user, "handle");
        }

        return null;
    }

    private static Uri? ReadArtworkUri(JsonElement item)
    {
        if (!item.TryGetProperty("artwork", out var artwork) ||
            artwork.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var sizeKey in ArtworkSizeKeys)
        {
            var value = ReadString(artwork, sizeKey);
            if (IsPublicHttpsUri(value, out var uri)) return uri;
        }

        return null;
    }

    private static bool TryBuildPermalink(string? permalink, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(permalink)) return false;

        var trimmed = permalink.Trim();
        if (trimmed.StartsWith("https://audius.co/", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) &&
            absolute.Scheme == Uri.UriSchemeHttps)
        {
            uri = absolute;
            return true;
        }

        if (!trimmed.StartsWith('/')) return false;

        return Uri.TryCreate(
            $"{CanonicalSiteUrl}{trimmed}",
            UriKind.Absolute,
            out uri);
    }

    private static bool IsPublicHttpsUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (Uri.CheckHostName(parsed.Host) != UriHostNameType.Dns) return false;

        uri = parsed;
        return true;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(
        JsonElement element,
        string property,
        bool fallback)
    {
        if (!element.TryGetProperty(property, out var value)) return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    /// <summary>
    /// Reads the response with a hard size ceiling so a misbehaving mirror
    /// cannot stream an unbounded body into memory.
    /// </summary>
    private static async Task<byte[]> ReadBoundedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await content
                .ReadAsync(chunk, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;

            if (buffer.Length + read > MaximumResponseBytes)
            {
                throw new HttpRequestException(
                    "The catalog response exceeded the maximum accepted size.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
