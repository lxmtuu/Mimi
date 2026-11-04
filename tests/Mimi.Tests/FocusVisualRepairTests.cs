using System.Windows;
using System.Windows.Controls;
using Mimi.Services;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// The focus-ring fault is special: WPF raises it from inside the input
/// pipeline when keyboard focus moves, the user did nothing to cause it, and a
/// dialog offers them nothing to do. So the app recognises it, puts a real value
/// on the one element that failed, and logs which element that was - the log
/// line is how the underlying style gets found.
/// </summary>
public sealed class FocusVisualRepairTests
{
    /// <summary>The message WPF produces, copied from the owner's screenshot.</summary>
    private const string WpfMessage =
        "'{DependencyProperty.UnsetValue}' is not a valid value for property " +
        "'FocusVisualStyle'.";

    [Fact]
    public void TheFaultIsRecognisedByItsMessage()
    {
        Assert.True(FocusVisualRepair.IsUnsetFocusVisualStyle(
            new InvalidOperationException(WpfMessage)));
    }

    [Fact]
    public void TheFaultIsRecognisedThroughAWrapper()
    {
        var wrapped = new Exception(
            "outer",
            new InvalidOperationException(WpfMessage));

        Assert.True(FocusVisualRepair.IsUnsetFocusVisualStyle(wrapped));
    }

    [Fact]
    public void TheFaultIsRecognisedWhenTheSurroundingTextIsLocalised()
    {
        // Windows translates the framework's own sentences on some locales, so
        // the match must lean on the property name rather than on the wording.
        var fault = new InvalidOperationException(
            "'{DependencyProperty.UnsetValue}' không phải giá trị hợp lệ cho " +
            "thuộc tính 'FocusVisualStyle'.");

        Assert.True(FocusVisualRepair.IsUnsetFocusVisualStyle(fault));
    }

    [Fact]
    public void OtherInvalidOperationExceptionsAreLeftAlone()
    {
        // Typed explicitly: three different exception types give the compiler
        // no common candidate to infer from.
        foreach (var exception in new Exception[]
                 {
                     new InvalidOperationException("The list is empty."),
                     new NullReferenceException(WpfMessage),
                     new ArgumentException(WpfMessage)
                 })
        {
            Assert.False(FocusVisualRepair.IsUnsetFocusVisualStyle(exception));
        }

        Assert.False(FocusVisualRepair.IsUnsetFocusVisualStyle(null));
    }

    [Fact]
    public void RepairSetsAValueThatCanBeReadBack()
    {
        RunOnSta(() =>
        {
            var button = new Button { Name = "PlayButton" };

            // Before the repair, no local value: the style's setter would be
            // what a reader saw.
            Assert.Equal(
                DependencyProperty.UnsetValue,
                button.ReadLocalValue(FrameworkElement.FocusVisualStyleProperty));

            Assert.True(FocusVisualRepair.Repair(button));

            // A local value now exists, so the framework never reaches the
            // setter that held the unset value. Reading it must not throw.
            Assert.NotEqual(
                DependencyProperty.UnsetValue,
                button.ReadLocalValue(FrameworkElement.FocusVisualStyleProperty));
            _ = button.FocusVisualStyle;
        });
    }

    [Fact]
    public void IdentifyNamesTheElementAndItsStyle()
    {
        RunOnSta(() =>
        {
            var button = new Button { Name = "PlayButton" };
            var label = FocusVisualRepair.Identify(button);

            Assert.Contains("Button", label, StringComparison.Ordinal);
            Assert.Contains("PlayButton", label, StringComparison.Ordinal);

            // An unnamed element still has to be identifiable.
            var anonymous = FocusVisualRepair.Identify(new TextBox());
            Assert.Contains("TextBox", anonymous, StringComparison.Ordinal);
            Assert.DoesNotContain("''", anonymous, StringComparison.Ordinal);

            Assert.Equal(
                "no focused element",
                FocusVisualRepair.Identify(null));
        });
    }

    [Fact]
    public void TheCrashHandlerRepairsBeforeItShowsAnyDialog()
    {
        var app = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Mimi.App",
            "App.xaml.cs"));

        // Scoped to the handler: the file shows a different dialog earlier, at
        // startup, which must not be mistaken for this one.
        var handler = app.IndexOf(
            "private void App_DispatcherUnhandledException",
            StringComparison.Ordinal);
        Assert.True(handler > 0, "The dispatcher handler must exist.");

        var repair = app.IndexOf(
            "FocusVisualRepair.TryRepair(e.Exception, out var repairedElement)",
            handler,
            StringComparison.Ordinal);
        var dialog = app.IndexOf("MessageBox.Show", handler, StringComparison.Ordinal);

        Assert.True(repair > handler, "The handler must try the repair.");
        Assert.True(dialog > handler, "The handler must still be able to report.");
        Assert.True(
            repair < dialog,
            "The repair decides whether a dialog is needed, so it has to run first.");

        // A repair that says nothing would trade one blind spot for another.
        Assert.Contains(
            "Recovered from an empty focus ring on {Element}",
            app,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheDialogCarriesTheBuildThatProducedIt()
    {
        var app = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Mimi.App",
            "App.xaml.cs"));

        // Two screenshots of the same fault are indistinguishable, and one of
        // them may already contain the fix.
        Assert.Contains(
            "{CrashReport.Summary(e.Exception)} [{BuildInfo.Display}]",
            app,
            StringComparison.Ordinal);
    }

    private static void RunOnSta(Action action)
    {
        // A failure inside the STA thread would otherwise be swallowed by the
        // join and the assertion would look like it passed.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new Xunit.Sdk.XunitException(
                $"The STA check failed: {failure}");
    }

    private static string RepositoryRoot()
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
