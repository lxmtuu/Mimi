using System.Net;
using System.Net.Http;
using System.Text;
using Mimi.Services;

namespace Mimi.Tests;

/// <summary>
/// Behaviour tests for the Audius catalog client: only credential-free
/// streaming metadata, rights filtering before results reach the UI, and a
/// hard ceiling on what the client is willing to read.
/// </summary>
public sealed class AudiusCatalogServiceTests
{
    private const string PlayableTrack = """
        {
          "id": "track-1",
          "title": "Neon Skyline",
          "duration": 214,
          "is_streamable": true,
          "is_stream_gated": false,
          "is_available": true,
          "permalink": "/mimi_artist/neon-skyline",
          "license": "OML",
          "artwork": { "480x480": "https://creatornode.audius.co/art/one.jpg" },
          "user": { "name": "Mimi Artist", "handle": "mimi_artist" }
        }
        """;

    [Fact]
    public async Task SearchKeepsOnlyTracksMimiMayStream()
    {
        var payload = $$"""
            {
              "data": [
                {{PlayableTrack}},
                {
                  "id": "gated",
                  "title": "Gated Song",
                  "duration": 180,
                  "is_streamable": true,
                  "is_stream_gated": true,
                  "permalink": "/someone/gated",
                  "user": { "name": "Someone" }
                },
                {
                  "id": "unstreamable",
                  "title": "No Stream",
                  "duration": 180,
                  "is_streamable": false,
                  "permalink": "/someone/no-stream",
                  "user": { "name": "Someone" }
                },
                {
                  "id": "app-locked",
                  "title": "App Locked",
                  "duration": 180,
                  "is_streamable": true,
                  "allowed_api_keys": ["0xabc"],
                  "permalink": "/someone/app-locked",
                  "user": { "name": "Someone" }
                },
                {
                  "id": "unavailable",
                  "title": "Unavailable",
                  "duration": 180,
                  "is_streamable": true,
                  "is_available": false,
                  "permalink": "/someone/unavailable",
                  "user": { "name": "Someone" }
                },
                {
                  "id": "no-artist",
                  "title": "Unknown Artist",
                  "duration": 180,
                  "is_streamable": true,
                  "permalink": "/someone/unknown",
                  "user": { }
                },
                {
                  "id": "no-duration",
                  "title": "No Duration",
                  "duration": 0,
                  "is_streamable": true,
                  "permalink": "/someone/no-duration",
                  "user": { "name": "Someone" }
                }
              ]
            }
            """;

        var handler = new RecordingHandler(HttpStatusCode.OK, payload);
        var service = CreateService(handler);

        var tracks = await service.SearchTracksAsync("neon", CancellationToken.None);

        var track = Assert.Single(tracks);
        Assert.Equal("track-1", track.Id);
        Assert.Equal("Neon Skyline", track.Title);
        Assert.Equal("Mimi Artist", track.Artist);
        Assert.Equal(214, track.Duration.TotalSeconds);
        Assert.Equal("OML", track.License);
        Assert.Equal(
            "https://audius.co/mimi_artist/neon-skyline",
            track.Permalink.AbsoluteUri);
        Assert.NotNull(track.ArtworkUrl);
        Assert.Equal("https://creatornode.audius.co/art/one.jpg", track.ArtworkUrl!.AbsoluteUri);
        Assert.Contains("track-1", track.StreamUrl.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/stream", track.StreamUrl.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchUsesTheDocumentedPublicEndpointsWithoutCredentials()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            $$"""{"data":[{{PlayableTrack}}]}""");
        var service = CreateService(handler);

        await service.SearchTracksAsync("neon skyline", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.StartsWith(
            "https://api.audius.co/v1/tracks/search?",
            request.RequestUri!.AbsoluteUri);
        Assert.Contains("query=neon%20skyline", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("app_name=Mimi", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Null(request.Headers.Authorization);
        Assert.DoesNotContain(
            "Bearer",
            request.Headers.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlankQueryMakesNoRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"data":[]}""");
        var service = CreateService(handler);

        var tracks = await service.SearchTracksAsync("   ", CancellationToken.None);

        Assert.Empty(tracks);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UnsuccessfulResponseThrowsInsteadOfReturningEmptyResults()
    {
        var handler = new RecordingHandler(HttpStatusCode.TooManyRequests, "{}");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SearchTracksAsync("neon", CancellationToken.None));
    }

    [Fact]
    public async Task MalformedPayloadYieldsNoResults()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "\"not an object\"");
        var service = CreateService(handler);

        Assert.Empty(await service.SearchTracksAsync("neon", CancellationToken.None));
    }

    private static AudiusCatalogService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });

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
}
