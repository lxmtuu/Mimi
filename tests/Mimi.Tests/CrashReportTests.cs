using System.IO;
using Mimi.Services;

namespace Mimi.Tests;

/// <summary>
/// The crash report is what the owner can actually send: a screenshot of the
/// dialog has to name the fault, and a repeat has to be recognised so a loop
/// cannot stack dialogs on top of the app.
/// </summary>
public sealed class CrashReportTests
{
    [Fact]
    public void TheSummaryNamesTheFaultItsMessageAndItsPlace()
    {
        var exception = Capture(() => ThrowInvalidOperation("the list changed"));

        var summary = CrashReport.Summary(exception);

        Assert.StartsWith("InvalidOperationException: the list changed", summary);
        Assert.Contains("ThrowInvalidOperation", summary);
    }

    [Fact]
    public void AWrapperReportsTheRealFault()
    {
        var exception = Capture(
            () => throw new AggregateException(
                "outer",
                new IOException("the file is busy")));

        var summary = CrashReport.Summary(exception);

        // The wrapper is what reached the handler, but the useful sentence is
        // the one from the fault it was carrying.
        Assert.Contains("the file is busy", summary);
        Assert.Contains("IOException", summary);
        Assert.Contains("AggregateException", summary);
    }

    [Fact]
    public void AMultiLineMessageStaysOnOneLine()
    {
        var exception = Capture(
            () => ThrowInvalidOperation("first line\nsecond line\r\nthird"));

        var summary = CrashReport.Summary(exception);

        Assert.False(summary.Contains('\n'), summary);
        Assert.False(summary.Contains('\r'), summary);
        Assert.Contains("first line second line third", summary);
    }

    [Fact]
    public void AVeryLongMessageIsCutToFitTheDialog()
    {
        var exception = Capture(() => ThrowInvalidOperation(new string('x', 2000)));

        var summary = CrashReport.Summary(exception);

        Assert.True(
            summary.Length <= CrashReport.MaximumDialogCharacters + 1,
            $"The summary grew to {summary.Length} characters.");
        Assert.EndsWith("…", summary);
    }

    [Fact]
    public void TheSameFaultTwiceSharesASignature()
    {
        var first = Capture(() => ThrowInvalidOperation("first value"));
        var second = Capture(() => ThrowInvalidOperation("second value"));

        // Different messages, same throw site: from the user's point of view
        // this is one bug, so it is reported once.
        Assert.Equal(CrashReport.Signature(first), CrashReport.Signature(second));
    }

    [Fact]
    public void DifferentFaultsDoNotShareASignature()
    {
        var invalid = Capture(() => ThrowInvalidOperation("boom"));
        var nullReference = Capture(ThrowNullReference);

        Assert.NotEqual(
            CrashReport.Signature(invalid),
            CrashReport.Signature(nullReference));
    }

    [Fact]
    public void TheSignatureKeepsTheInnermostType()
    {
        var wrapped = Capture(
            () => throw new InvalidOperationException(
                "wrapper",
                new KeyNotFoundException("inner")));

        Assert.Contains("KeyNotFoundException", CrashReport.Signature(wrapped));
        Assert.Contains("InvalidOperationException", CrashReport.Signature(wrapped));
    }

    [Fact]
    public void TheDialogGetsTheSummaryInBothLanguages()
    {
        var root = FindRepositoryRoot();
        var english = File.ReadAllText(
            Path.Combine(root, "src", "Mimi.App", "Localization", "UiStrings.resx"));
        var vietnamese = File.ReadAllText(
            Path.Combine(root, "src", "Mimi.App", "Localization", "UiStrings.vi.resx"));

        // The dialog text is user-facing, so it has to exist in both, and the
        // placeholder is what carries the fault into it.
        Assert.Contains(
            "<data name=\"App_UnexpectedProblemDetails\"",
            english,
            StringComparison.Ordinal);
        Assert.Contains(
            "<data name=\"App_UnexpectedProblemDetails\"",
            vietnamese,
            StringComparison.Ordinal);
        Assert.Contains("Details: {0}", english, StringComparison.Ordinal);
        Assert.Contains("Chi tiết: {0}", vietnamese, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAppReportsTheFaultAndStopsRepeatingItself()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "Mimi.App", "App.xaml.cs"));

        Assert.Contains("CrashReport.Signature(e.Exception)", app, StringComparison.Ordinal);
        Assert.Contains("CrashReport.Summary(e.Exception)", app, StringComparison.Ordinal);
        Assert.Contains("reportedCrashSignatures.Add(signature)", app, StringComparison.Ordinal);

        // The early return is what keeps a looping fault from burying the app
        // under a tower of message boxes.
        Assert.Contains("if (!isFirstOccurrence) return;", app, StringComparison.Ordinal);
    }

    [Fact]
    public void LoopsThatRunUnattendedCannotRaiseTheDialog()
    {
        var root = FindRepositoryRoot();

        var visualizer = File.ReadAllText(
            Path.Combine(root, "src", "Mimi.App", "Controls", "VisualizerView.cs"));
        Assert.Contains(
            "The visualiser could not draw a frame",
            visualizer,
            StringComparison.Ordinal);
        Assert.Contains(
            "reportedDrawFailures.Add(CrashReport.Signature(ex))",
            visualizer,
            StringComparison.Ordinal);

        // The lyrics list scrolls on every lyric line; its attempt has to be
        // guarded, including the layout call that used to sit outside the try.
        var scroll = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Mimi.App",
            "Behaviors",
            "ScrollSelectedItemIntoViewBehavior.cs"));
        var call = scroll.IndexOf("listBox.ScrollIntoView(selectedItem);", StringComparison.Ordinal);
        Assert.True(call > 0);

        // A "try" that opens before the call and is still open at the call is
        // the only thing that matters here.
        var tryIndex = scroll.LastIndexOf("try", call, StringComparison.Ordinal);
        Assert.True(tryIndex >= 0, "ScrollIntoView must sit inside a try.");
        Assert.DoesNotContain("}", scroll[tryIndex..call], StringComparison.Ordinal);
        Assert.Contains("listBox.UpdateLayout();", scroll[call..], StringComparison.Ordinal);
    }

    private static void ThrowInvalidOperation(string message) =>
        throw new InvalidOperationException(message);

    private static void ThrowNullReference() =>
        throw new NullReferenceException("nothing here");

    private static Exception Capture(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new Xunit.Sdk.XunitException("Expected the action to throw.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mimi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            "Could not locate the repository root (Mimi.sln) above " +
            AppContext.BaseDirectory);
    }
}
