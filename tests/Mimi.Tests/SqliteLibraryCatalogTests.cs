using Microsoft.Data.Sqlite;
using Mimi.Models;
using Mimi.Services;

namespace Mimi.Tests;

public sealed class SqliteLibraryCatalogTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "MimiCatalogTests-" + Guid.NewGuid().ToString("N"));
    private readonly string databasePath;
    private readonly SqliteLibraryCatalog catalog;

    public SqliteLibraryCatalogTests()
    {
        Directory.CreateDirectory(root);
        databasePath = Path.Combine(root, "library.db");
        catalog = new SqliteLibraryCatalog(databasePath);
    }

    [Fact]
    public async Task InitializeAndUpsert_PersistsTracksAcrossCatalogInstances()
    {
        await catalog.InitializeAsync();
        var path = CreateAudioFile("neon-night.mp3");
        await catalog.UpsertTracksAsync([
            CreateTrack(
                path,
                "Đêm Neon",
                artist: "Mimi Diva",
                album: "Sân Khấu Ánh Sáng",
                year: 2026)
        ]);

        using var reopened = new SqliteLibraryCatalog(databasePath);
        await reopened.InitializeAsync();
        var loaded = Assert.Single(await reopened.LoadTracksAsync());

        Assert.Equal(path, loaded.FilePath);
        Assert.Equal("Đêm Neon", loaded.Title);
        Assert.Equal("Mimi Diva", loaded.Artist);
        Assert.Equal("Sân Khấu Ánh Sáng", loaded.Album);
        Assert.Equal(2026, loaded.Year);
        Assert.Equal(320, loaded.BitrateKbps);
        Assert.Equal(48000, loaded.SampleRateHz);
        Assert.False(loaded.IsMissing);

        Assert.Equal(
            path,
            Assert.Single(await reopened.SearchTracksAsync("dem neon")).FilePath);
        Assert.Equal(
            path,
            Assert.Single(await reopened.SearchTracksAsync("san khau")).FilePath);
    }

    [Fact]
    public async Task UpsertAgain_PreservesFavoriteStatsAndAddedDate()
    {
        await catalog.InitializeAsync();
        var path = CreateAudioFile("favorite.mp3");
        var addedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        var original = CreateTrack(path, "Original Title", addedAt: addedAt);
        original.IsFavorite = true;
        await catalog.UpsertTracksAsync([original]);

        var historyId = await catalog.RecordPlaybackStartedAsync(path);
        Assert.True(historyId > 0);
        Assert.True(await catalog.MarkPlaybackCompletedAsync(historyId));

        var rescanned = CreateTrack(
            path,
            "Updated Title",
            addedAt: DateTimeOffset.UtcNow);
        rescanned.IsFavorite = false;
        rescanned.PlayCount = 99;
        rescanned.LastPlayedAtUtc = null;
        await catalog.UpsertTracksAsync([rescanned]);

        var loaded = Assert.Single(await catalog.LoadTracksAsync());
        Assert.Equal("Updated Title", loaded.Title);
        Assert.True(loaded.IsFavorite);
        Assert.Equal(1, loaded.PlayCount);
        Assert.Equal(addedAt, loaded.AddedAtUtc);
        Assert.NotNull(loaded.LastPlayedAtUtc);
    }

    [Fact]
    public async Task FavoriteErrorAndRemove_UpdateCatalog()
    {
        await catalog.InitializeAsync();
        var keepPath = CreateAudioFile("keep.mp3");
        var removePath = CreateAudioFile("remove.mp3");
        await catalog.UpsertTracksAsync([
            CreateTrack(keepPath, "Keep This Song"),
            CreateTrack(removePath, "Remove This Song")
        ]);

        Assert.True(await catalog.SetFavoriteAsync(keepPath, isFavorite: true));
        Assert.True(await catalog.SetPlaybackErrorAsync(keepPath, hasError: true));

        var kept = (await catalog.LoadTracksAsync())
            .Single(track => track.FilePath == keepPath);
        Assert.True(kept.IsFavorite);
        Assert.True(kept.HasError);

        Assert.True(await catalog.RemoveTrackAsync(removePath));
        Assert.Equal(
            keepPath,
            Assert.Single(await catalog.LoadTracksAsync()).FilePath);
        Assert.Empty(await catalog.SearchTracksAsync("Remove This"));
    }

    [Fact]
    public async Task RecordPlayback_LoadRecentTracksReturnsMostRecentFirst()
    {
        await catalog.InitializeAsync();
        var firstPath = CreateAudioFile("first.mp3");
        var secondPath = CreateAudioFile("second.mp3");
        var unplayedPath = CreateAudioFile("unplayed.mp3");
        await catalog.UpsertTracksAsync([
            CreateTrack(firstPath, "First"),
            CreateTrack(secondPath, "Second"),
            CreateTrack(unplayedPath, "Unplayed")
        ]);

        Assert.True(await catalog.RecordPlaybackStartedAsync(firstPath) > 0);
        Assert.True(await catalog.RecordPlaybackStartedAsync(secondPath) > 0);

        var recent = await catalog.LoadRecentTracksAsync();
        Assert.Equal(
            new[] { secondPath, firstPath },
            recent.Select(track => track.FilePath));
        Assert.Equal(
            secondPath,
            Assert.Single(await catalog.LoadRecentTracksAsync(maxCount: 1)).FilePath);
    }

    [Fact]
    public async Task Playlists_CrudMoveAndRemoveMaintainOrder()
    {
        await catalog.InitializeAsync();
        var firstPath = CreateAudioFile("one.mp3");
        var secondPath = CreateAudioFile("two.mp3");
        var thirdPath = CreateAudioFile("three.mp3");
        await catalog.UpsertTracksAsync([
            CreateTrack(firstPath, "One"),
            CreateTrack(secondPath, "Two"),
            CreateTrack(thirdPath, "Three")
        ]);

        var playlist = await catalog.CreatePlaylistAsync("  Stage Mix  ");
        Assert.Equal("Stage Mix", playlist.Name);
        Assert.True(await catalog.AddTrackToPlaylistAsync(playlist.Id, firstPath));
        Assert.True(await catalog.AddTrackToPlaylistAsync(playlist.Id, secondPath));
        Assert.True(await catalog.AddTrackToPlaylistAsync(playlist.Id, thirdPath));
        Assert.False(await catalog.AddTrackToPlaylistAsync(playlist.Id, firstPath));

        var loadedPlaylist = Assert.Single(await catalog.LoadPlaylistsAsync());
        Assert.Equal(3, loadedPlaylist.TrackCount);

        Assert.True(await catalog.MovePlaylistTrackAsync(
            playlist.Id,
            fromPosition: 0,
            toPosition: 2));
        Assert.Equal(
            new[] { secondPath, thirdPath, firstPath },
            (await catalog.LoadPlaylistTracksAsync(playlist.Id))
                .Select(track => track.FilePath));

        Assert.True(await catalog.RemoveTrackFromPlaylistAsync(playlist.Id, thirdPath));
        Assert.Equal(
            new[] { secondPath, firstPath },
            (await catalog.LoadPlaylistTracksAsync(playlist.Id))
                .Select(track => track.FilePath));

        Assert.True(await catalog.RenamePlaylistAsync(playlist.Id, "Encore"));
        Assert.Equal(
            "Encore",
            Assert.Single(await catalog.LoadPlaylistsAsync()).Name);

        Assert.True(await catalog.DeletePlaylistAsync(playlist.Id));
        Assert.Empty(await catalog.LoadPlaylistsAsync());
    }

    public void Dispose()
    {
        catalog.Dispose();
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private string CreateAudioFile(string fileName)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private Track CreateTrack(
        string path,
        string title,
        string artist = "",
        string album = "",
        int? year = null,
        DateTimeOffset? addedAt = null)
    {
        var info = new FileInfo(path);
        return new Track
        {
            FilePath = path,
            MetadataTitle = title,
            Artist = artist,
            Album = album,
            Year = year,
            Duration = TimeSpan.FromMinutes(3),
            BitrateKbps = 320,
            SampleRateHz = 48000,
            FileSizeBytes = info.Length,
            FileModifiedUtc = new DateTimeOffset(
                info.LastWriteTimeUtc,
                TimeSpan.Zero),
            AddedAtUtc = addedAt ?? DateTimeOffset.UtcNow,
            MetadataSource = "tag"
        };
    }
}
