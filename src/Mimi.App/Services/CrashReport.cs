using System.Text;

namespace Mimi.Services;

/// <summary>
/// Turns an exception into something a person can report and a developer can
/// act on.
///
/// The dialog used to say only "an unexpected problem happened", which is
/// useless from a screenshot: the log has the detail, but asking the owner to
/// find a log file for every crash is a bad trade. The summary puts the
/// exception type, its message and where it was thrown straight into the
/// dialog, and the signature lets the app recognise a repeat so a fault inside
/// a per-frame loop cannot open a modal dialog sixty times a second.
/// </summary>
public static class CrashReport
{
    /// <summary>
    /// How much of the message reaches the dialog. Enough to identify the
    /// fault, short enough to stay readable in a message box.
    /// </summary>
    public const int MaximumDialogCharacters = 320;

    /// <summary>
    /// A stable identity for "the same fault". Two throws from the same type at
    /// the same place share a signature even when their messages differ, which
    /// is what makes de-duplication work for loops.
    /// </summary>
    public static string Signature(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var innermost = Innermost(exception);
        var site = exception.TargetSite;
        var location = site?.DeclaringType is { } declaring
            ? $"{declaring.FullName}.{site.Name}"
            : site?.Name ?? "unknown-site";

        return $"{exception.GetType().Name}@{location}/{innermost.GetType().Name}";
    }

    /// <summary>
    /// One short paragraph for the dialog: what failed, what it said, and where.
    /// Newlines are collapsed so a multi-line exception cannot stretch the box.
    /// </summary>
    public static string Summary(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var innermost = Innermost(exception);
        var builder = new StringBuilder();

        // "AggregateException: IOException: the file is busy" says both what
        // reached the handler and what actually failed. Naming only the wrapper
        // would hide the one word a developer searches for.
        builder.Append(exception.GetType().Name);
        if (!ReferenceEquals(innermost, exception))
        {
            builder.Append(": ").Append(innermost.GetType().Name);
        }

        var message = Collapse(innermost.Message);
        if (message.Length > 0)
            builder.Append(": ").Append(message);

        if (exception.TargetSite is { } site)
        {
            var type = site.DeclaringType?.Name;
            builder.Append(type is null ? " — at " : $" — at {type}.")
                .Append(site.Name);
        }

        var text = builder.ToString();
        return text.Length <= MaximumDialogCharacters
            ? text
            : text[..MaximumDialogCharacters] + "…";
    }

    private static Exception Innermost(Exception exception)
    {
        var current = exception;
        while (current.InnerException is { } inner)
            current = inner;

        return current;
    }

    private static string Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            builder.Append(c);
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }
}
