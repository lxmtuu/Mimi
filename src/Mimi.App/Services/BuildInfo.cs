using System.Reflection;

namespace Mimi.Services;

/// <summary>
/// Which build is running. A theme bug report is only actionable when the
/// answer is known: the same screenshot can come from a build with the fix and
/// one without it, and the difference between them is invisible on screen.
/// </summary>
public static class BuildInfo
{
    private static readonly Assembly Assembly = typeof(BuildInfo).Assembly;

    /// <summary>
    /// The assembly version, e.g. <c>0.6.0</c>.
    /// </summary>
    public static string Version { get; } =
        Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// The short commit the build came from, or <c>local</c> when the build was
    /// not produced from a git checkout.
    /// </summary>
    public static string Commit { get; } = ReadCommit();

    /// <summary>
    /// One line for the log and the footer: <c>0.6.0 · 9f1331c</c>.
    /// </summary>
    public static string Display =>
        string.Equals(Commit, "local", StringComparison.Ordinal)
            ? Version
            : $"{Version} · {Commit}";

    private static string ReadCommit()
    {
        var informational = Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational)) return "local";

        var separator = informational.IndexOf('+', StringComparison.Ordinal);
        if (separator < 0 || separator == informational.Length - 1) return "local";

        var revision = informational[(separator + 1)..].Trim();
        return revision.Length > 7 ? revision[..7] : revision;
    }
}
