using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Persistent music catalog. SQLite is the Phase 3 source of truth for
/// tracks, favorites, playlists, play history, and accent-insensitive FTS5
/// search. Legacy JSON remains only as a migration source and fallback.
/// </summary>
public interface ILibraryCatalog
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>> LoadTracksAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task UpsertTracksAsync(
        IEnumerable<Track> tracks,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveTrackAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<bool> SetFavoriteAsync(
        string filePath,
        bool isFavorite,
        CancellationToken cancellationToken = default);

    Task<bool> SetPlaybackErrorAsync(
        string filePath,
        bool hasError,
        CancellationToken cancellationToken = default);

    /// <summary>Starts a play-history row and returns its ID, or 0 if unknown.</summary>
    Task<long> RecordPlaybackStartedAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<bool> MarkPlaybackCompletedAsync(
        long playHistoryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>> LoadRecentTracksAsync(
        int maxCount = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Playlist>> LoadPlaylistsAsync(
        CancellationToken cancellationToken = default);

    Task<Playlist> CreatePlaylistAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> RenamePlaylistAsync(
        long playlistId,
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePlaylistAsync(
        long playlistId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>> LoadPlaylistTracksAsync(
        long playlistId,
        CancellationToken cancellationToken = default);

    Task<bool> AddTrackToPlaylistAsync(
        long playlistId,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveTrackFromPlaylistAsync(
        long playlistId,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<bool> MovePlaylistTrackAsync(
        long playlistId,
        int fromPosition,
        int toPosition,
        CancellationToken cancellationToken = default);
}
