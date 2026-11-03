using System.Net;
using System.Net.Http;
using System.Text;
using Mimi.Models;
using Mimi.Services;
using Mimi.YouTube;

namespace Mimi.Tests;

/// <summary>
/// The YouTube slice is search-only: a user-supplied Data API key, metadata
/// results, no stream resolution, and an honest quota refusal.
/// </summary>
public sealed class YouTubeSearchServiceTests
{
    /// <summary>A key-shaped value: Mimi validates shape on both key sources.</summary>
    private const string TestKey = "AIzaSyA1234567890abcdefghijklmnopqrstu";

    [Fact]
    public async Task SearchReturnsVideoMetadataOnly()
    {
        const string payload = """
            {
              "items": [
                {
                  "id": { "kind": "youtube#video", "videoId": "abc123" },
                  "snippet": {
                    "title": "Neon Skyline",
                    "channelTitle": "Mimi Artist",
                    "thumbnails": {
                      "default": { "url": "https://i.ytimg.com/vi/abc123/default.jpg" },
                      "medium": { "url": "https://i.ytimg.com/vi/abc123/mqdefault.jpg" }
                    }
                  }
                },
                {
                  "id": { "kind": "youtube#channel", "channelId": "chan1" },
                  "snippet": { "title": "A channel" }
                }
              ]
            }
            """;

        var handler = new RecordingHandler(HttpStatusCode.OK, payload);
        var service = CreateService(handler, TestKey);

        var videos = await service.SearchAsync("neon", CancellationToken.None);

        var video = Assert.Single(videos);
        Assert.Equal("abc123", video.VideoId);
        Assert.Equal("Neon Skyline", video.Title);
        Assert.Equal("Mimi Artist", video.ChannelTitle);
        Assert.Equal(
            "https://i.ytimg.com/vi/abc123/mqdefault.jpg",
            video.ThumbnailUrl!.AbsoluteUri);
        Assert.Equal(
            "https://www.youtube.com/watch?v=abc123",
            video.WatchUrl.AbsoluteUri);
    }

    [Theory]
    [InlineData("dQw4w9WgXcQ", true)]
    [InlineData("abc123", false)]
    [InlineData("dQw4w9WgXc", false)]
    [InlineData("dQw4w9WgXc!", false)]
    public void OnlyYouTubeShapedIdsReachThePlayerPage(string videoId, bool expected) =>
        Assert.Equal(
            expected,
            new YouTubeVideo(videoId, "title", "channel", null).HasValidVideoId);

    [Fact]
    public async Task SearchUsesTheOfficialDataApiWithTheUserKey()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"items":[]}""");
        var service = CreateService(handler, TestKey);

        await service.SearchAsync("neon skyline", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.StartsWith(
            "https://www.googleapis.com/youtube/v3/search?",
            request.RequestUri!.AbsoluteUri);
        Assert.Contains("q=neon%20skyline", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("type=video", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("key=" + TestKey, request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingKeyHidesTheFeatureInsteadOfFailing()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"items":[]}""");
        var service = CreateService(handler, apiKey: null);

        Assert.False(service.IsConfigured);
        Assert.Empty(await service.SearchAsync("neon", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ExhaustedQuotaIsReportedAsItsOwnState()
    {
        var handler = new RecordingHandler(HttpStatusCode.Forbidden, "{}");
        var service = CreateService(handler, TestKey);

        await Assert.ThrowsAsync<YouTubeQuotaExceededException>(() =>
            service.SearchAsync("neon", CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    [InlineData("AIzaSyA1234567890 abcdefghijklmnop")]
    public void KeyShapeRejectsCopyPasteDamage(string? raw) =>
        Assert.Null(YouTubeApiKey.Normalize(raw));

    [Fact]
    public void ADamagedKeyFileCountsAsNotConfigured()
    {
        // The key file is written by hand from a terminal, so the most likely
        // mistake is a stray space or a line break. Sending that would only
        // produce a 400, so it degrades to "not configured" instead.
        var directory = Path.Combine(
            Path.GetTempPath(),
            "mimi-youtube-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(
                Path.Combine(directory, YouTubeSearchService.KeyFileName),
                "AIzaSyA1234567890 abcdefghijklmnop\n");

            var service = new YouTubeSearchService(
                new FakeUserDataService(directory),
                new HttpClient(new RecordingHandler(HttpStatusCode.OK, "{}")),
                _ => null);

            Assert.False(service.IsConfigured);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ARefusedKeyIsReportedSeparatelyFromANetworkFailure()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, "{}");
        var service = CreateService(handler, "AIzaSyA1234567890abcdefghijklmnopqrstu");

        await Assert.ThrowsAsync<YouTubeInvalidKeyException>(() =>
            service.SearchAsync("neon", CancellationToken.None));
    }

    private static YouTubeSearchService CreateService(
        HttpMessageHandler handler,
        string? apiKey) =>
        new(
            new FakeUserDataService(),
            new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) },
            _ => apiKey);

    private sealed class RecordingHandler(
        HttpStatusCode status,
        string payload) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                new HttpResponseMessage(status)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
        }
    }

    private sealed class FakeUserDataService(string? dataDirectory = null)
        : IUserDataService
    {
        public string DataDirectory => dataDirectory ??
            Path.Combine(Path.GetTempPath(), "mimi-youtube-tests");

        public void MigrateLegacyDataIfNeeded() { }
        public AppSettings LoadSettings() => new();
        public void SaveSettings(AppSettings settings) { }
        public IReadOnlyList<string> LoadLibraryPaths() => [];
        public void SaveLibraryPaths(IEnumerable<string> paths) { }
        public HashSet<string> LoadFavorites() => new(StringComparer.OrdinalIgnoreCase);
        public void SaveFavorites(IEnumerable<string> paths) { }
        public void ArchiveImportedLibraryData() { }
    }
}
