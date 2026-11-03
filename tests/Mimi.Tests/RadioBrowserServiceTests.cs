using System.Net;
using System.Net.Http;
using Mimi.Services;

namespace Mimi.Tests;

public sealed class RadioBrowserServiceTests
{
    private static readonly Guid ValidStationUuid =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ParseStationsFiltersUnsafeBrokenAndDuplicateEntries()
    {
        var json = $$"""
            [
              {
                "stationuuid": "{{ValidStationUuid:D}}",
                "name": "  Mimi   Wave  ",
                "url_resolved": "https://stream.example/live.mp3",
                "homepage": "https://station.example/",
                "favicon": "https://station.example/icon.png",
                "country": "Japan",
                "language": "Japanese",
                "tags": " anime, J-Pop, anime, , lofi ",
                "codec": "MP3",
                "bitrate": "128",
                "votes": -5,
                "clickcount": 42,
                "lastcheckok": 1,
                "hls": 0
              },
              {
                "stationuuid": "{{ValidStationUuid:D}}",
                "name": "Duplicate",
                "url_resolved": "https://stream.example/duplicate.mp3",
                "lastcheckok": 1,
                "hls": 0
              },
              {
                "stationuuid": "22222222-2222-2222-2222-222222222222",
                "name": "Broken",
                "url_resolved": "https://stream.example/broken.mp3",
                "lastcheckok": 0,
                "hls": 0
              },
              {
                "stationuuid": "33333333-3333-3333-3333-333333333333",
                "name": "HLS",
                "url_resolved": "https://stream.example/live.m3u8",
                "lastcheckok": 1,
                "hls": 1
              },
              {
                "stationuuid": "44444444-4444-4444-4444-444444444444",
                "name": "FTP",
                "url_resolved": "ftp://stream.example/live.mp3",
                "lastcheckok": 1,
                "hls": 0
              },
              {
                "stationuuid": "55555555-5555-5555-5555-555555555555",
                "name": "Credential URL",
                "url_resolved": "https://user:password@stream.example/live.mp3",
                "lastcheckok": 1,
                "hls": 0
              }
            ]
            """;

        var stations = RadioBrowserService.ParseStations(json);

        var station = Assert.Single(stations);
        Assert.Equal(ValidStationUuid, station.StationUuid);
        Assert.Equal("Mimi Wave", station.Name);
        Assert.Equal("https://stream.example/live.mp3", station.StreamUrl.AbsoluteUri);
        Assert.Equal("https://station.example/", station.HomepageUrl?.AbsoluteUri);
        Assert.Equal("https://station.example/icon.png", station.FaviconUrl?.AbsoluteUri);
        Assert.Equal(["anime", "J-Pop", "lofi"], station.Tags);
        Assert.Equal(128, station.BitrateKbps);
        Assert.Equal(0, station.Votes);
        Assert.Equal(42, station.ClickCount);
    }

    [Fact]
    public async Task SearchStationsSendsBoundedHealthyQueryWithMimiUserAgent()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse(ValidStationJson()));
        using var service = CreateService(handler, new Uri("https://mirror.api.radio-browser.info/"));

        var stations = await service.SearchStationsAsync("  lofi & chill  ");

        Assert.Single(stations);
        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Equal("/json/stations/search", requestUri.AbsolutePath);
        Assert.Contains("limit=60", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("hidebroken=true", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("order=votes", requestUri.Query, StringComparison.Ordinal);
        Assert.Contains("lofi%20%26%20chill", requestUri.Query, StringComparison.Ordinal);
        Assert.Equal("Mimi/9.8.7", Assert.Single(handler.UserAgents));
    }

    [Fact]
    public async Task BlankSearchUsesTopStationsEndpoint()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse("[]"));
        using var service = CreateService(handler, new Uri("https://mirror.api.radio-browser.info/"));

        var stations = await service.SearchStationsAsync("   ");

        Assert.Empty(stations);
        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Equal("/json/stations/topvote/60", requestUri.AbsolutePath);
        Assert.Contains("hidebroken=true", requestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogRequestsFailOverToHealthyMirror()
    {
        var handler = new RecordingHandler((request, _) =>
            request.RequestUri?.Host == "one.api.radio-browser.info"
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse("[]"));
        using var service = CreateService(
            handler,
            new Uri("https://one.api.radio-browser.info/"),
            new Uri("https://two.api.radio-browser.info/"));

        var stations = await service.GetTopStationsAsync();

        Assert.Empty(stations);
        Assert.Equal(
            ["one.api.radio-browser.info", "two.api.radio-browser.info"],
            handler.RequestUris.Select(uri => uri.Host).ToArray());
    }

    [Fact]
    public async Task RegisterStationClickUsesExactlyOneRequest()
    {
        var handler = new RecordingHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var service = CreateService(
            handler,
            new Uri("https://one.api.radio-browser.info/"),
            new Uri("https://two.api.radio-browser.info/"));

        await Assert.ThrowsAsync<RadioServiceException>(() =>
            service.RegisterStationClickAsync(ValidStationUuid));

        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Equal("one.api.radio-browser.info", requestUri.Host);
        Assert.Equal($"/json/url/{ValidStationUuid:D}", requestUri.AbsolutePath);
    }

    [Fact]
    public async Task CatalogRequestsIgnoreNonRadioBrowserMirrors()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse("[]"));
        using var service = CreateService(
            handler,
            new Uri("https://evil.example/"),
            new Uri("https://api.radio-browser.info.evil.example/"),
            new Uri("http://de1.api.radio-browser.info/"),
            new Uri("https://de2.api.radio-browser.info/"));

        var stations = await service.GetTopStationsAsync();

        Assert.Empty(stations);
        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Equal("de2.api.radio-browser.info", requestUri.Host);
    }

    [Theory]
    [InlineData("http://localhost/live.mp3")]
    [InlineData("http://127.0.0.1/live.mp3")]
    [InlineData("http://10.0.0.5/live.mp3")]
    [InlineData("http://172.16.0.5/live.mp3")]
    [InlineData("http://192.168.1.20/live.mp3")]
    [InlineData("http://169.254.10.20/live.mp3")]
    [InlineData("http://[::1]/live.mp3")]
    [InlineData("http://[fd00::1]/live.mp3")]
    [InlineData("http://[fe80::1]/live.mp3")]
    [InlineData("http://printer.local/live.mp3")]
    public void ParseStationsFiltersNonPublicStreamHosts(string streamUrl)
    {
        var json = ValidStationJson().Replace(
            "https://stream.example/live.mp3",
            streamUrl,
            StringComparison.Ordinal);

        Assert.Empty(RadioBrowserService.ParseStations(json));
    }

    [Theory]
    [InlineData("https://8.8.8.8/live.mp3")]
    [InlineData("http://172.15.0.5/live.mp3")]
    [InlineData("http://172.32.0.5/live.mp3")]
    public void ParseStationsAllowsPublicStreamHosts(string streamUrl)
    {
        var json = ValidStationJson().Replace(
            "https://stream.example/live.mp3",
            streamUrl,
            StringComparison.Ordinal);

        Assert.Single(RadioBrowserService.ParseStations(json));
    }

    [Theory]
    [InlineData("https://stream.example/live.m3u8")]
    [InlineData("https://stream.example/live.m3u")]
    [InlineData("https://stream.example/live.M3U")]
    [InlineData("https://stream.example/listen.pls?sid=1")]
    [InlineData("https://stream.example/live.xspf")]
    public void ParseStationsFiltersPlaylistStreamUrls(string streamUrl)
    {
        var json = ValidStationJson().Replace(
            "https://stream.example/live.mp3",
            streamUrl,
            StringComparison.Ordinal);

        Assert.Empty(RadioBrowserService.ParseStations(json));
    }

    [Theory]
    [InlineData("https://stream.example/live.mp3?next=.m3u")]
    [InlineData("https://stream.example/mount.pls.mp3")]
    [InlineData("https://stream.example/m3u")]
    public void ParseStationsAllowsDirectStreamUrls(string streamUrl)
    {
        var json = ValidStationJson().Replace(
            "https://stream.example/live.mp3",
            streamUrl,
            StringComparison.Ordinal);

        Assert.Single(RadioBrowserService.ParseStations(json));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    public void ParseStationsRejectsInvalidResponses(string json) =>
        Assert.Throws<RadioServiceException>(() =>
            RadioBrowserService.ParseStations(json));

    private static RadioBrowserService CreateService(
        RecordingHandler handler,
        params Uri[] mirrors)
    {
        var httpClient = new HttpClient(handler);
        return new RadioBrowserService(
            httpClient,
            _ => Task.FromResult<IReadOnlyList<Uri>>(mirrors),
            (_, _) => Task.CompletedTask,
            "9.8.7");
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

    private static string ValidStationJson() => $$"""
        [
          {
            "stationuuid": "{{ValidStationUuid:D}}",
            "name": "Mimi Wave",
            "url_resolved": "https://stream.example/live.mp3",
            "homepage": "https://station.example/",
            "country": "Japan",
            "language": "Japanese",
            "tags": "anime",
            "codec": "MP3",
            "bitrate": 128,
            "votes": 10,
            "clickcount": 20,
            "lastcheckok": 1,
            "hls": 0
          }
        ]
        """;

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(responder(request, RequestUris.Count));
        }
    }
}
