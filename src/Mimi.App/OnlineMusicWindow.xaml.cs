using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Mimi.Localization;
using Mimi.Services;
using Serilog;

namespace Mimi;

/// <summary>
/// Hosts the official YouTube Music web player in an isolated browser
/// surface. Mimi only navigates the real site: it does not block ads, alter
/// the player, extract streams, download audio, or keep its own cache of
/// audio, metadata, or artwork. Closing the window tears the browser down,
/// which stops playback.
/// </summary>
public partial class OnlineMusicWindow : Window
{
    public const string OnlineMusicStartUrl = "https://music.youtube.com/";

    private const string WebView2RuntimeUrl =
        "https://developer.microsoft.com/microsoft-edge/webview2/";

    public OnlineMusicWindow(LocalizationService? localizationService = null)
    {
        Loc = localizationService ?? new LocalizationService();
        InitializeComponent();
        DataContext = this;

        Title = Loc["Online_WindowTitle"];
        HeaderTitle.Text = Loc["Online_Title"];
        HeaderSubtitle.Text = Loc["Online_LegalNotice"];
        StatusHeadline.Text = Loc["Online_Loading"];

        Loaded += OnlineMusicWindow_Loaded;
        Closed += OnlineMusicWindow_Closed;
    }

    public LocalizationService Loc { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowChromeHelper.ApplyDarkTitleBar(this);
    }

    private async void OnlineMusicWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnlineMusicWindow_Loaded;
        await InitializeBrowserAsync();
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            // The profile keeps the user's own site session (sign-in, queue)
            // between Mimi runs, exactly like a browser profile. Mimi adds no
            // audio, metadata, or artwork cache of its own.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Mimi",
                "OnlineMusicProfile");
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);
            await Browser.EnsureCoreWebView2Async(environment);

            var core = Browser.CoreWebView2
                ?? throw new InvalidOperationException(
                    "The embedded browser did not finish starting.");
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsSwipeNavigationEnabled = false;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.DownloadStarting += Core_DownloadStarting;
            core.NavigationCompleted += Core_NavigationCompleted;

            StatusPanel.Visibility = Visibility.Collapsed;
            Browser.Visibility = Visibility.Visible;
            Browser.Source = new Uri(OnlineMusicStartUrl);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowFailure(
                Loc["Online_RuntimeMissing"],
                Loc["Online_RuntimeMissingDetail"],
                showRuntimeButton: true);
        }
        catch (Exception ex)
        {
            Log.Warning(
                "Could not start the online music browser: {ExceptionType}",
                ex.GetType().FullName);
            ShowFailure(Loc["Online_LoadFailed"], Loc["Online_LegalNotice"], false);
        }
    }

    private void ShowFailure(
        string headline,
        string detail,
        bool showRuntimeButton)
    {
        Browser.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = Visibility.Visible;
        StatusProgress.Visibility = Visibility.Collapsed;
        StatusHeadline.Text = headline;
        StatusDetail.Text = detail;
        RuntimeInstallButton.Visibility = showRuntimeButton
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// The embedded site stays inside this window; links that ask for a new
    /// window are handed to the user's own browser instead of spawning
    /// untracked Mimi surfaces.
    /// </summary>
    private void Core_NewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        TryOpenExternally(e.Uri);
    }

    /// <summary>
    /// Mimi never stores media. A download request is declined rather than
    /// silently saved somewhere the user cannot see.
    /// </summary>
    private void Core_DownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
    }

    private void Core_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess) return;

        ShowFailure(Loc["Online_LoadFailed"], Loc["Online_LegalNotice"], false);
    }

    private void OpenInBrowser_Click(object sender, RoutedEventArgs e) =>
        TryOpenExternally(OnlineMusicStartUrl);

    private void RuntimeInstall_Click(object sender, RoutedEventArgs e) =>
        TryOpenExternally(WebView2RuntimeUrl);

    private static void TryOpenExternally(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Debug(
                "Could not hand a URL to the default browser: {ExceptionType}",
                ex.GetType().FullName);
        }
    }

    private void OnlineMusicWindow_Closed(object? sender, EventArgs e)
    {
        Closed -= OnlineMusicWindow_Closed;
        Browser.Dispose();
    }
}
