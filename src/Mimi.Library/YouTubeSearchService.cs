using System.Net.Http.Headers;
using System.Text.Json;
using Mimi.YouTube;

namespace Mimi.Services;

/// <summary>
/// Search-only client for the official YouTube Data API v3.
///
/// The API key belongs to the user: it is read from the environment or from a
/// file in the user's own data folder, never from source, and it is never
/// logged. A missing key only hides the feature. This client cannot resolve a
/// stream URL, so Mimi can never play YouTube audio outside the official
/// player that the user is looking at.
/// </summary>
public sealed class YouTubeSearchService : IYouTubeSearchService
{
    public const string ApiEndpoint =
        "https://www.googleapis.com/youtube/v3/search";

    public const string EnvironmentVariable = "MIMI_YOUTUBE_API_KEY";

    public const string KeyFileName = "youtube-api-key.txt";

    /// <summary>search.list costs 100 of the default 10,000 daily units.</summary>
    public const int MaximumResults = 20;

    private readonly HttpClient httpClient;

    private readonly IUserDataService userDataService;

    private readonly Func<string, string?> environmentReader;

    public YouTubeSearchService(
        IUserDataService userDataService,
        HttpClient? httpClient = null,
        Func<string, string?>? environmentReader = null)
    {
        this.userDataService = userDataService;
        this.environmentReader =
            environmentReader ?? Environment.GetEnvironmentVariable;
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

    public string KeyFilePath =>
        Path.Combine(userDataService.DataDirectory, KeyFileName);

    public bool IsConfigured => ResolveApiKey() is not null;

    public async Task<IReadOnlyList<YouTubeVideo>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return [];

        var apiKey = ResolveApiKey();
        if (apiKey is null) return [];

        var requestUri =
            $"{ApiEndpoint}?part=snippet&type=video" +
            $"&maxResults={MaximumResults}" +
            $"&q={Uri.EscapeDataString(trimmed)}" +
            $"&key={Uri.EscapeDataString(apiKey)}";

        using var response = await httpClient
            .GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is
            System.Net.HttpStatusCode.Forbidden or
            System.Net.HttpStatusCode.TooManyRequests)
        {
            // 403 is how the Data API reports an exhausted daily quota.
            throw new YouTubeQuotaExceededException(
                "The YouTube Data API quota for this key is exhausted.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"YouTube answered {(int)response.StatusCode}.");
        }

        await using var content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var document = await JsonDocument
            .ParseAsync(content, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ParseVideos(document.RootElement);
    }

    /// <summary>
    /// Reads the key lazily on every search so dropping the file in is enough:
    /// the user does not have to restart Mimi. Nothing here is cached or
    /// written back.
    /// </summary>
    private string? ResolveApiKey()
    {
        var fromEnvironment = environmentReader(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment.Trim();

        try
        {
            var path = KeyFilePath;
            if (!File.Exists(path)) return null;

            var fromFile = File.ReadAllText(path).Trim();
            return fromFile.Length == 0 ? null : fromFile;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            // A protected or unreadable file must not break the rest of Mimi.
            return null;
        }
    }

    internal static IReadOnlyList<YouTubeVideo> ParseVideos(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var videos = new List<YouTubeVideo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items.EnumerateArray())
        {
            if (videos.Count >= MaximumResults) break;
            if (item.ValueKind != JsonValueKind.Object) continue;

            if (!item.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.Object ||
                !id.TryGetProperty("videoId", out var videoId) ||
                videoId.ValueKind != JsonValueKind.String)
            {
                // Channels and playlists come back with a different id shape.
                continue;
            }

            var identifier = videoId.GetString();
            if (string.IsNullOrWhiteSpace(identifier)) continue;
            if (!seen.Add(identifier)) continue;

            var title = string.Empty;
            var channel = string.Empty;
            Uri? thumbnail = null;

            if (item.TryGetProperty("snippet", out var snippet) &&
                snippet.ValueKind == JsonValueKind.Object)
            {
                title = ReadString(snippet, "title") ?? string.Empty;
                channel = ReadString(snippet, "channelTitle") ?? string.Empty;
                thumbnail = ReadThumbnail(snippet);
            }

            if (title.Length == 0) continue;

            videos.Add(new YouTubeVideo(identifier, title, channel, thumbnail));
        }

        return videos;
    }

    private static Uri? ReadThumbnail(JsonElement snippet)
    {
        if (!snippet.TryGetProperty("thumbnails", out var thumbnails) ||
            thumbnails.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Prefer the higher-resolution variants, then fall back.
        foreach (var name in new[] { "medium", "high", "default" })
        {
            if (!thumbnails.TryGetProperty(name, out var entry) ||
                entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var url = ReadString(entry, "url");
            if (string.IsNullOrWhiteSpace(url)) continue;

            if (Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                parsed.Scheme == Uri.UriSchemeHttps)
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
