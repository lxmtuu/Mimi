using Microsoft.Data.Sqlite;
using Mimi.Core.Text;
using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// SQLite implementation of the Phase 3 library catalog. Writes are
/// serialized inside the process and performed in transactions; reads use
/// short-lived connections against the WAL database.
/// </summary>
public sealed class SqliteLibraryCatalog : ILibraryCatalog, IDisposable
{
    private const int CurrentSchemaVersion = 1;
    private const string TrackColumns =
        "id, file_path, title, artist, album, album_artist, genre, year, " +
        "duration_ms, bitrate_kbps, sample_rate_hz, file_size, " +
        "file_mtime_utc_ms, added_at_utc_ms, last_played_at_utc_ms, " +
        "play_count, is_favorite, is_missing, has_error, cover_art_path, " +
        "metadata_source";

    private readonly string connectionString;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private bool disposed;

    public SqliteLibraryCatalog(IUserDataService userDataService)
        : this(Path.Combine(userDataService.DataDirectory, "library.db"))
    {
    }

    internal SqliteLibraryCatalog(string databasePath)
    {
        DatabasePath = databasePath;
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        };
        connectionString = builder.ToString();
    }

    internal string DatabasePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var connection = await OpenConnectionAsync(cancellationToken);

            await using (var migrationsTable = connection.CreateCommand())
            {
                migrationsTable.CommandText = """
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        version INTEGER PRIMARY KEY,
                        applied_at_utc_ms INTEGER NOT NULL
                    );
                    """;
                await migrationsTable.ExecuteNonQueryAsync(cancellationToken);
            }

            long currentVersion;
            await using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.CommandText =
                    "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
                var value = await versionCommand.ExecuteScalarAsync(cancellationToken);
                currentVersion = Convert.ToInt64(value);
            }

            if (currentVersion >= CurrentSchemaVersion) return;

            using var transaction = connection.BeginTransaction();
            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = SchemaVersion1Sql;
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = """
                    INSERT OR IGNORE INTO schema_migrations(version, applied_at_utc_ms)
                    VALUES ($version, $appliedAt);
                    """;
                Add(record, "$version", CurrentSchemaVersion);
                Add(record, "$appliedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public Task<IReadOnlyList<Track>> LoadTracksAsync(
        CancellationToken cancellationToken = default) =>
        QueryTracksAsync(
            $"SELECT {TrackColumns} FROM tracks ORDER BY added_at_utc_ms, id;",
            addParameters: null,
            cancellationToken);

    public async Task<IReadOnlyList<Track>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalized = TextSearch.Normalize(query);
        if (normalized.Length == 0)
            return await LoadTracksAsync(cancellationToken);

        var match = BuildFtsMatch(normalized);
        if (match.Length == 0) return [];

        var columns = string.Join(
            ", ",
            TrackColumns.Split(',')
                .Select(column => "t." + column.Trim()));

        return await QueryTracksAsync(
            $"""
             SELECT {columns}
             FROM tracks_fts
             JOIN tracks AS t ON t.id = tracks_fts.rowid
             WHERE tracks_fts MATCH $match
             ORDER BY bm25(tracks_fts), t.title COLLATE NOCASE;
             """,
            command => Add(command, "$match", match),
            cancellationToken);
    }

    public async Task UpsertTracksAsync(
        IEnumerable<Track> tracks,
        CancellationToken cancellationToken = default)
    {
        var tracksToSave = tracks.ToArray();
        if (tracksToSave.Length == 0) return;

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            foreach (var track in tracksToSave)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpsertTrackAsync(
                    connection,
                    transaction,
                    track,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<bool> RemoveTrackAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        await ExecuteWriteAsync(
            "DELETE FROM tracks WHERE file_path = $path COLLATE NOCASE;",
            command => Add(command, "$path", filePath),
            cancellationToken) > 0;

    public async Task<bool> SetFavoriteAsync(
        string filePath,
        bool isFavorite,
        CancellationToken cancellationToken = default) =>
        await ExecuteWriteAsync(
            """
            UPDATE tracks
            SET is_favorite = $isFavorite
            WHERE file_path = $path COLLATE NOCASE;
            """,
            command =>
            {
                Add(command, "$isFavorite", isFavorite ? 1 : 0);
                Add(command, "$path", filePath);
            },
            cancellationToken) > 0;

    public async Task<bool> SetPlaybackErrorAsync(
        string filePath,
        bool hasError,
        CancellationToken cancellationToken = default) =>
        await ExecuteWriteAsync(
            """
            UPDATE tracks
            SET has_error = $hasError
            WHERE file_path = $path COLLATE NOCASE;
            """,
            command =>
            {
                Add(command, "$hasError", hasError ? 1 : 0);
                Add(command, "$path", filePath);
            },
            cancellationToken) > 0;

    public async Task<long> RecordPlaybackStartedAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            var trackId = await GetTrackIdAsync(
                connection,
                transaction,
                filePath,
                cancellationToken);
            if (trackId == 0) return 0;

            var playedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE tracks
                    SET last_played_at_utc_ms = $playedAt,
                        play_count = play_count + 1,
                        is_missing = 0,
                        has_error = 0
                    WHERE id = $trackId;
                    """;
                Add(update, "$playedAt", playedAt);
                Add(update, "$trackId", trackId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            long historyId;
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO play_history(track_id, played_at_utc_ms, completed)
                    VALUES ($trackId, $playedAt, 0)
                    RETURNING id;
                    """;
                Add(insert, "$trackId", trackId);
                Add(insert, "$playedAt", playedAt);
                historyId = Convert.ToInt64(
                    await insert.ExecuteScalarAsync(cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return historyId;
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<bool> MarkPlaybackCompletedAsync(
        long playHistoryId,
        CancellationToken cancellationToken = default)
    {
        if (playHistoryId <= 0) return false;

        return await ExecuteWriteAsync(
            "UPDATE play_history SET completed = 1 WHERE id = $historyId;",
            command => Add(command, "$historyId", playHistoryId),
            cancellationToken) > 0;
    }

    public Task<IReadOnlyList<Track>> LoadRecentTracksAsync(
        int maxCount = 50,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(maxCount, 1, 500);
        return QueryTracksAsync(
            $"""
             SELECT {TrackColumns}
             FROM tracks
             WHERE last_played_at_utc_ms IS NOT NULL
             ORDER BY last_played_at_utc_ms DESC, id DESC
             LIMIT $limit;
             """,
            command => Add(command, "$limit", limit),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Playlist>> LoadPlaylistsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.name, p.created_at_utc_ms, p.updated_at_utc_ms,
                   COUNT(pt.track_id)
            FROM playlists AS p
            LEFT JOIN playlist_tracks AS pt ON pt.playlist_id = p.id
            GROUP BY p.id, p.name, p.created_at_utc_ms, p.updated_at_utc_ms
            ORDER BY p.name COLLATE NOCASE, p.id;
            """;

        var playlists = new List<Playlist>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            playlists.Add(ReadPlaylist(reader));

        return playlists;
    }

    public async Task<Playlist> CreatePlaylistAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var cleanName = ValidatePlaylistName(name);
        var now = DateTimeOffset.UtcNow;

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO playlists(name, created_at_utc_ms, updated_at_utc_ms)
                VALUES ($name, $createdAt, $updatedAt)
                RETURNING id;
                """;
            Add(command, "$name", cleanName);
            Add(command, "$createdAt", now.ToUnixTimeMilliseconds());
            Add(command, "$updatedAt", now.ToUnixTimeMilliseconds());

            var id = Convert.ToInt64(
                await command.ExecuteScalarAsync(cancellationToken));
            return new Playlist
            {
                Id = id,
                Name = cleanName,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                TrackCount = 0
            };
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<bool> RenamePlaylistAsync(
        long playlistId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var cleanName = ValidatePlaylistName(name);
        return await ExecuteWriteAsync(
            """
            UPDATE playlists
            SET name = $name, updated_at_utc_ms = $updatedAt
            WHERE id = $playlistId;
            """,
            command =>
            {
                Add(command, "$name", cleanName);
                Add(command, "$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Add(command, "$playlistId", playlistId);
            },
            cancellationToken) > 0;
    }

    public async Task<bool> DeletePlaylistAsync(
        long playlistId,
        CancellationToken cancellationToken = default) =>
        await ExecuteWriteAsync(
            "DELETE FROM playlists WHERE id = $playlistId;",
            command => Add(command, "$playlistId", playlistId),
            cancellationToken) > 0;

    public Task<IReadOnlyList<Track>> LoadPlaylistTracksAsync(
        long playlistId,
        CancellationToken cancellationToken = default)
    {
        var columns = string.Join(
            ", ",
            TrackColumns.Split(',')
                .Select(column => "t." + column.Trim()));

        return QueryTracksAsync(
            $"""
             SELECT {columns}
             FROM playlist_tracks AS pt
             JOIN tracks AS t ON t.id = pt.track_id
             WHERE pt.playlist_id = $playlistId
             ORDER BY pt.position;
             """,
            command => Add(command, "$playlistId", playlistId),
            cancellationToken);
    }

    public async Task<bool> AddTrackToPlaylistAsync(
        long playlistId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            if (!await PlaylistExistsAsync(
                    connection,
                    transaction,
                    playlistId,
                    cancellationToken))
            {
                return false;
            }

            var trackId = await GetTrackIdAsync(
                connection,
                transaction,
                filePath,
                cancellationToken);
            if (trackId == 0) return false;

            if (await PlaylistContainsTrackAsync(
                    connection,
                    transaction,
                    playlistId,
                    trackId,
                    cancellationToken))
            {
                return false;
            }

            long nextPosition;
            await using (var positionCommand = connection.CreateCommand())
            {
                positionCommand.Transaction = transaction;
                positionCommand.CommandText = """
                    SELECT COALESCE(MAX(position), -1) + 1
                    FROM playlist_tracks
                    WHERE playlist_id = $playlistId;
                    """;
                Add(positionCommand, "$playlistId", playlistId);
                nextPosition = Convert.ToInt64(
                    await positionCommand.ExecuteScalarAsync(cancellationToken));
            }

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO playlist_tracks(
                        playlist_id, track_id, position, added_at_utc_ms)
                    VALUES ($playlistId, $trackId, $position, $addedAt);
                    """;
                Add(insert, "$playlistId", playlistId);
                Add(insert, "$trackId", trackId);
                Add(insert, "$position", nextPosition);
                Add(insert, "$addedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await TouchPlaylistAsync(
                connection,
                transaction,
                playlistId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<bool> RemoveTrackFromPlaylistAsync(
        long playlistId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            var trackId = await GetTrackIdAsync(
                connection,
                transaction,
                filePath,
                cancellationToken);
            if (trackId == 0) return false;

            int affected;
            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM playlist_tracks
                    WHERE playlist_id = $playlistId AND track_id = $trackId;
                    """;
                Add(delete, "$playlistId", playlistId);
                Add(delete, "$trackId", trackId);
                affected = await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            if (affected == 0) return false;

            var remaining = await GetPlaylistTrackIdsAsync(
                connection,
                transaction,
                playlistId,
                cancellationToken);
            await RewritePlaylistPositionsAsync(
                connection,
                transaction,
                playlistId,
                remaining,
                cancellationToken);
            await TouchPlaylistAsync(
                connection,
                transaction,
                playlistId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<bool> MovePlaylistTrackAsync(
        long playlistId,
        int fromPosition,
        int toPosition,
        CancellationToken cancellationToken = default)
    {
        if (fromPosition < 0 || toPosition < 0) return false;

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            var trackIds = await GetPlaylistTrackIdsAsync(
                connection,
                transaction,
                playlistId,
                cancellationToken);
            if (fromPosition >= trackIds.Count || toPosition >= trackIds.Count)
                return false;
            if (fromPosition == toPosition) return true;

            var moving = trackIds[fromPosition];
            trackIds.RemoveAt(fromPosition);
            trackIds.Insert(toPosition, moving);

            await RewritePlaylistPositionsAsync(
                connection,
                transaction,
                playlistId,
                trackIds,
                cancellationToken);
            await TouchPlaylistAsync(
                connection,
                transaction,
                playlistId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        finally
        {
            writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        writeLock.Dispose();
    }

    private async Task UpsertTrackAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Track track,
        CancellationToken cancellationToken)
    {
        if (track.AddedAtUtc == default)
            track.AddedAtUtc = DateTimeOffset.UtcNow;

        track.IsMissing = !File.Exists(track.FilePath);

        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                SELECT id, added_at_utc_ms, last_played_at_utc_ms,
                       play_count, is_favorite
                FROM tracks
                WHERE file_path = $path COLLATE NOCASE
                LIMIT 1;
                """;
            Add(existingCommand, "$path", track.FilePath);

            await using var reader = await existingCommand
                .ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                track.Id = reader.GetInt64(0);
                track.AddedAtUtc = FromUnixMilliseconds(reader.GetInt64(1));
                track.LastPlayedAtUtc = GetNullableDateTimeOffset(reader, 2);
                track.PlayCount = reader.GetInt32(3);
                track.IsFavorite = reader.GetInt64(4) != 0;
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tracks (
                file_path, title, artist, album, album_artist, genre, year,
                duration_ms, bitrate_kbps, sample_rate_hz, file_size,
                file_mtime_utc_ms, added_at_utc_ms, last_played_at_utc_ms,
                play_count, is_favorite, is_missing, has_error,
                cover_art_path, metadata_source,
                title_normalized, artist_normalized, album_normalized)
            VALUES (
                $filePath, $title, $artist, $album, $albumArtist, $genre,
                $year, $durationMs, $bitrateKbps, $sampleRateHz, $fileSize,
                $fileModifiedMs, $addedAtMs, $lastPlayedMs, $playCount,
                $isFavorite, $isMissing, $hasError, $coverArtPath,
                $metadataSource, $titleNormalized, $artistNormalized,
                $albumNormalized)
            ON CONFLICT(file_path) DO UPDATE SET
                title = excluded.title,
                artist = excluded.artist,
                album = excluded.album,
                album_artist = excluded.album_artist,
                genre = excluded.genre,
                year = excluded.year,
                duration_ms = excluded.duration_ms,
                bitrate_kbps = excluded.bitrate_kbps,
                sample_rate_hz = excluded.sample_rate_hz,
                file_size = excluded.file_size,
                file_mtime_utc_ms = excluded.file_mtime_utc_ms,
                is_missing = excluded.is_missing,
                has_error = excluded.has_error,
                cover_art_path = excluded.cover_art_path,
                metadata_source = excluded.metadata_source,
                title_normalized = excluded.title_normalized,
                artist_normalized = excluded.artist_normalized,
                album_normalized = excluded.album_normalized
            RETURNING id;
            """;

        Add(command, "$filePath", track.FilePath);
        Add(command, "$title", track.Title);
        Add(command, "$artist", track.Artist);
        Add(command, "$album", track.Album);
        Add(command, "$albumArtist", track.AlbumArtist);
        Add(command, "$genre", track.Genre);
        Add(command, "$year", track.Year);
        Add(command, "$durationMs", ToDurationMilliseconds(track.Duration));
        Add(command, "$bitrateKbps", track.BitrateKbps);
        Add(command, "$sampleRateHz", track.SampleRateHz);
        Add(command, "$fileSize", track.FileSizeBytes);
        Add(command, "$fileModifiedMs", ToUnixMilliseconds(track.FileModifiedUtc));
        Add(command, "$addedAtMs", track.AddedAtUtc.ToUnixTimeMilliseconds());
        Add(command, "$lastPlayedMs", ToUnixMilliseconds(track.LastPlayedAtUtc));
        Add(command, "$playCount", Math.Max(0, track.PlayCount));
        Add(command, "$isFavorite", track.IsFavorite ? 1 : 0);
        Add(command, "$isMissing", track.IsMissing ? 1 : 0);
        Add(command, "$hasError", track.HasError ? 1 : 0);
        Add(command, "$coverArtPath", track.CoverArtPath);
        Add(command, "$metadataSource", track.MetadataSource);
        Add(command, "$titleNormalized", TextSearch.Normalize(track.Title));
        Add(command, "$artistNormalized", TextSearch.Normalize(track.Artist));
        Add(command, "$albumNormalized", TextSearch.Normalize(track.Album));

        track.Id = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<Track>> QueryTracksAsync(
        string sql,
        Action<SqliteCommand>? addParameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        addParameters?.Invoke(command);

        var tracks = new List<Track>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tracks.Add(ReadTrack(reader));

        return tracks;
    }

    private async Task<int> ExecuteWriteAsync(
        string sql,
        Action<SqliteCommand> addParameters,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            addParameters(command);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
                PRAGMA foreign_keys = ON;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<long> GetTrackIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT id FROM tracks WHERE file_path = $path COLLATE NOCASE LIMIT 1;";
        Add(command, "$path", filePath);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private static async Task<bool> PlaylistExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long playlistId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM playlists WHERE id = $playlistId;";
        Add(command, "$playlistId", playlistId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> PlaylistContainsTrackAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long playlistId,
        long trackId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT 1
            FROM playlist_tracks
            WHERE playlist_id = $playlistId AND track_id = $trackId;
            """;
        Add(command, "$playlistId", playlistId);
        Add(command, "$trackId", trackId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<List<long>> GetPlaylistTrackIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long playlistId,
        CancellationToken cancellationToken)
    {
        var trackIds = new List<long>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT track_id
            FROM playlist_tracks
            WHERE playlist_id = $playlistId
            ORDER BY position;
            """;
        Add(command, "$playlistId", playlistId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            trackIds.Add(reader.GetInt64(0));

        return trackIds;
    }

    private static async Task RewritePlaylistPositionsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long playlistId,
        IReadOnlyList<long> trackIds,
        CancellationToken cancellationToken)
    {
        await using (var offset = connection.CreateCommand())
        {
            offset.Transaction = transaction;
            offset.CommandText = """
                UPDATE playlist_tracks
                SET position = position + 1000000
                WHERE playlist_id = $playlistId;
                """;
            Add(offset, "$playlistId", playlistId);
            await offset.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var position = 0; position < trackIds.Count; position++)
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE playlist_tracks
                SET position = $position
                WHERE playlist_id = $playlistId AND track_id = $trackId;
                """;
            Add(update, "$position", position);
            Add(update, "$playlistId", playlistId);
            Add(update, "$trackId", trackIds[position]);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task TouchPlaylistAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long playlistId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE playlists
            SET updated_at_utc_ms = $updatedAt
            WHERE id = $playlistId;
            """;
        Add(command, "$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Add(command, "$playlistId", playlistId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Track ReadTrack(SqliteDataReader reader)
    {
        var track = new Track
        {
            Id = reader.GetInt64(0),
            FilePath = reader.GetString(1),
            MetadataTitle = GetNullableString(reader, 2),
            Artist = GetStringOrEmpty(reader, 3),
            Album = GetStringOrEmpty(reader, 4),
            AlbumArtist = GetStringOrEmpty(reader, 5),
            Genre = GetStringOrEmpty(reader, 6),
            Year = GetNullableInt32(reader, 7),
            Duration = GetNullableDuration(reader, 8),
            BitrateKbps = GetNullableInt32(reader, 9),
            SampleRateHz = GetNullableInt32(reader, 10),
            FileSizeBytes = reader.GetInt64(11),
            FileModifiedUtc = GetNullableDateTimeOffset(reader, 12),
            AddedAtUtc = FromUnixMilliseconds(reader.GetInt64(13)),
            LastPlayedAtUtc = GetNullableDateTimeOffset(reader, 14),
            PlayCount = reader.GetInt32(15),
            IsFavorite = reader.GetInt64(16) != 0,
            IsMissing = reader.GetInt64(17) != 0,
            HasError = reader.GetInt64(18) != 0,
            CoverArtPath = GetNullableString(reader, 19),
            MetadataSource = GetStringOrEmpty(reader, 20)
        };

        if (!File.Exists(track.FilePath))
            track.IsMissing = true;

        return track;
    }

    private static Playlist ReadPlaylist(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        CreatedAtUtc = FromUnixMilliseconds(reader.GetInt64(2)),
        UpdatedAtUtc = FromUnixMilliseconds(reader.GetInt64(3)),
        TrackCount = Convert.ToInt32(reader.GetInt64(4))
    };

    private static string BuildFtsMatch(string normalizedQuery)
    {
        var terms = normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => new string(term.Where(char.IsLetterOrDigit).ToArray()))
            .Where(term => term.Length > 0)
            .Select(term => $"\"{term.Replace("\"", "\"\"")}\"*");

        return string.Join(" AND ", terms);
    }

    private static string ValidatePlaylistName(string name)
    {
        var clean = name?.Trim() ?? string.Empty;
        return clean.Length == 0
            ? throw new ArgumentException("Playlist name cannot be empty.", nameof(name))
            : clean;
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static long? ToDurationMilliseconds(TimeSpan? duration) =>
        duration is { } value && value > TimeSpan.Zero
            ? (long)Math.Round(value.TotalMilliseconds)
            : null;

    private static long? ToUnixMilliseconds(DateTimeOffset? value) =>
        value?.ToUnixTimeMilliseconds();

    private static DateTimeOffset FromUnixMilliseconds(long value) =>
        DateTimeOffset.FromUnixTimeMilliseconds(value);

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string GetStringOrEmpty(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static int? GetNullableInt32(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static TimeSpan? GetNullableDuration(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : TimeSpan.FromMilliseconds(reader.GetInt64(ordinal));

    private static DateTimeOffset? GetNullableDateTimeOffset(
        SqliteDataReader reader,
        int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : FromUnixMilliseconds(reader.GetInt64(ordinal));

    private const string SchemaVersion1Sql = """
        CREATE TABLE IF NOT EXISTS tracks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            file_path TEXT NOT NULL COLLATE NOCASE UNIQUE,
            title TEXT NOT NULL,
            artist TEXT NOT NULL DEFAULT '',
            album TEXT NOT NULL DEFAULT '',
            album_artist TEXT NOT NULL DEFAULT '',
            genre TEXT NOT NULL DEFAULT '',
            year INTEGER NULL,
            duration_ms INTEGER NULL,
            bitrate_kbps INTEGER NULL,
            sample_rate_hz INTEGER NULL,
            file_size INTEGER NOT NULL DEFAULT 0,
            file_mtime_utc_ms INTEGER NULL,
            added_at_utc_ms INTEGER NOT NULL,
            last_played_at_utc_ms INTEGER NULL,
            play_count INTEGER NOT NULL DEFAULT 0,
            is_favorite INTEGER NOT NULL DEFAULT 0 CHECK (is_favorite IN (0, 1)),
            is_missing INTEGER NOT NULL DEFAULT 0 CHECK (is_missing IN (0, 1)),
            has_error INTEGER NOT NULL DEFAULT 0 CHECK (has_error IN (0, 1)),
            cover_art_path TEXT NULL,
            metadata_source TEXT NOT NULL DEFAULT 'filename',
            title_normalized TEXT NOT NULL DEFAULT '',
            artist_normalized TEXT NOT NULL DEFAULT '',
            album_normalized TEXT NOT NULL DEFAULT ''
        );

        CREATE INDEX IF NOT EXISTS idx_tracks_last_played
            ON tracks(last_played_at_utc_ms DESC);
        CREATE INDEX IF NOT EXISTS idx_tracks_favorite
            ON tracks(is_favorite);
        CREATE INDEX IF NOT EXISTS idx_tracks_title_normalized
            ON tracks(title_normalized);

        CREATE TABLE IF NOT EXISTS playlists (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            created_at_utc_ms INTEGER NOT NULL,
            updated_at_utc_ms INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS playlist_tracks (
            playlist_id INTEGER NOT NULL,
            track_id INTEGER NOT NULL,
            position INTEGER NOT NULL,
            added_at_utc_ms INTEGER NOT NULL,
            PRIMARY KEY (playlist_id, position),
            FOREIGN KEY (playlist_id) REFERENCES playlists(id) ON DELETE CASCADE,
            FOREIGN KEY (track_id) REFERENCES tracks(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_playlist_tracks_track
            ON playlist_tracks(track_id);

        CREATE TABLE IF NOT EXISTS play_history (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            track_id INTEGER NOT NULL,
            played_at_utc_ms INTEGER NOT NULL,
            completed INTEGER NOT NULL DEFAULT 0 CHECK (completed IN (0, 1)),
            FOREIGN KEY (track_id) REFERENCES tracks(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_play_history_track_time
            ON play_history(track_id, played_at_utc_ms DESC);

        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value_json TEXT NOT NULL
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS tracks_fts USING fts5(
            title,
            artist,
            album,
            title_normalized,
            artist_normalized,
            album_normalized,
            content='tracks',
            content_rowid='id'
        );

        CREATE TRIGGER IF NOT EXISTS tracks_fts_ai
        AFTER INSERT ON tracks BEGIN
            INSERT INTO tracks_fts(
                rowid, title, artist, album,
                title_normalized, artist_normalized, album_normalized)
            VALUES (
                new.id, new.title, new.artist, new.album,
                new.title_normalized, new.artist_normalized,
                new.album_normalized);
        END;

        CREATE TRIGGER IF NOT EXISTS tracks_fts_ad
        AFTER DELETE ON tracks BEGIN
            INSERT INTO tracks_fts(
                tracks_fts, rowid, title, artist, album,
                title_normalized, artist_normalized, album_normalized)
            VALUES (
                'delete', old.id, old.title, old.artist, old.album,
                old.title_normalized, old.artist_normalized,
                old.album_normalized);
        END;

        CREATE TRIGGER IF NOT EXISTS tracks_fts_au
        AFTER UPDATE ON tracks BEGIN
            INSERT INTO tracks_fts(
                tracks_fts, rowid, title, artist, album,
                title_normalized, artist_normalized, album_normalized)
            VALUES (
                'delete', old.id, old.title, old.artist, old.album,
                old.title_normalized, old.artist_normalized,
                old.album_normalized);
            INSERT INTO tracks_fts(
                rowid, title, artist, album,
                title_normalized, artist_normalized, album_normalized)
            VALUES (
                new.id, new.title, new.artist, new.album,
                new.title_normalized, new.artist_normalized,
                new.album_normalized);
        END;

        INSERT INTO tracks_fts(tracks_fts) VALUES('rebuild');
        """;
}
