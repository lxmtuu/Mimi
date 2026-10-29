namespace Mimi.Models;

/// <summary>User settings persisted to settings.json. Kept intentionally
/// small in v0.1.1; richer settings arrive with the Phase 3 settings system.
/// <see cref="Version"/> lets future builds migrate the file safely.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public double Volume { get; set; } = 0.7;
}
