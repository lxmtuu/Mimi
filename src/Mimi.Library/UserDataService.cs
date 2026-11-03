using System.Text.Json;
using Mimi.Models;

namespace Mimi.Services;

/// <summary>JSON persistence for the library, favorites, and app settings.</summary>
public sealed class UserDataService : IUserDataService
{
    private const string FavoritesFileName = "favorites.json";
    private const string LibraryFileName = "library.json";
    private const string SettingsFileName = "settings.json";

    private readonly string legacyDataDirectory;

    public UserDataService()
        : this(DefaultDataDirectory(), DefaultLegacyDataDirectory())
    {
    }

    internal UserDataService(string dataDirectory, string legacyDataDirectory)
    {
        DataDirectory = dataDirectory;
        this.legacyDataDirectory = legacyDataDirectory;
    }

    public string DataDirectory { get; }

    private string FavoritesFile => Path.Combine(DataDirectory, FavoritesFileName);
    private string LibraryFile => Path.Combine(DataDirectory, LibraryFileName);
    private string SettingsFile => Path.Combine(DataDirectory, SettingsFileName);

    private static string DefaultDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mimi");

    // Mimi was previously named "MikuVerse Music". This legacy folder name is
    // kept only so existing library/favorites data can be migrated.
    private static string DefaultLegacyDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MikuVerseMusic");

    public void MigrateLegacyDataIfNeeded()
    {
        try
        {
            if (!Directory.Exists(legacyDataDirectory)) return;

            Directory.CreateDirectory(DataDirectory);
            foreach (var fileName in new[] { FavoritesFileName, LibraryFileName })
            {
                var source = Path.Combine(legacyDataDirectory, fileName);
                var target = Path.Combine(DataDirectory, fileName);
                if (File.Exists(source) && !File.Exists(target))
                    File.Copy(source, target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the player usable if legacy data cannot be copied.
        }
    }

    public AppSettings LoadSettings()
    {
        try
        {
            var settings = File.Exists(SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile))
                : null;
            settings ??= new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (
            ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            settings.Normalize();
            Directory.CreateDirectory(DataDirectory);
            WriteAllTextAtomic(SettingsFile, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are best-effort; never block playback on them.
        }
    }

    public IReadOnlyList<string> LoadLibraryPaths()
    {
        if (!File.Exists(LibraryFile)) return [];
        return JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(LibraryFile)) ?? [];
    }

    public void SaveLibraryPaths(IEnumerable<string> paths)
    {
        Directory.CreateDirectory(DataDirectory);
        WriteAllTextAtomic(
            LibraryFile,
            JsonSerializer.Serialize(paths.ToArray()));
    }

    public HashSet<string> LoadFavorites()
    {
        if (!File.Exists(FavoritesFile))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var saved = JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(FavoritesFile));
        return new HashSet<string>(
            saved ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    public void SaveFavorites(IEnumerable<string> paths)
    {
        Directory.CreateDirectory(DataDirectory);
        WriteAllTextAtomic(
            FavoritesFile,
            JsonSerializer.Serialize(paths.ToArray()));
    }

    public void ArchiveImportedLibraryData()
    {
        ArchiveFile(LibraryFile);
        ArchiveFile(FavoritesFile);
    }

    private static void ArchiveFile(string path)
    {
        if (!File.Exists(path)) return;

        var backupPath = path + ".bak";
        if (File.Exists(backupPath))
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backupPath = $"{path}.bak-{timestamp}";
        }

        File.Move(path, backupPath);
    }

    // Write via a temp file + replace so a crash mid-write cannot leave a
    // truncated file behind.
    internal static void WriteAllTextAtomic(string path, string contents)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, path, overwrite: true);
    }
}
