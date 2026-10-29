using Mimi.Models;

namespace Mimi.Services;

/// <summary>
/// Persists Mimi's small JSON documents in the per-user data directory.
/// File writes must be atomic so an interrupted write cannot corrupt the
/// library, favorites, or settings.
/// </summary>
public interface IUserDataService
{
    string DataDirectory { get; }

    /// <summary>Copies pre-rename MikuVerseMusic data once, without overwriting.</summary>
    void MigrateLegacyDataIfNeeded();

    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);

    IReadOnlyList<string> LoadLibraryPaths();
    void SaveLibraryPaths(IEnumerable<string> paths);

    HashSet<string> LoadFavorites();
    void SaveFavorites(IEnumerable<string> paths);
}
