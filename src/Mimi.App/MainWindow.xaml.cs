using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Mimi.Controls;
using Mimi.Services;
using Mimi.ViewModels;

namespace Mimi;

/// <summary>
/// View glue only: playback/library behavior lives in
/// <see cref="MainWindowViewModel"/>. This class wires view-specific pieces
/// (visualizer frame callback, progress-thumb drag state, fullscreen Stage,
/// and render metrics) that do not belong in a view model.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly IAudioPlayer audioPlayer;
    private readonly DispatcherTimer metricsTimer =
        new() { Interval = TimeSpan.FromMilliseconds(500) };
    private StageWindow? stageWindow;
    private WindowsMediaControlsService? mediaControls;
    private IntPtr mainWindowHandle;
    private bool youTubeBrowserReady;
    private bool youTubePollInFlight;
    private int embeddedPlayerSilences;
    private int reportedEmbeddedPlayerError;

    /// <summary>
    /// Host name mapped to a local folder. YouTube's embedded player requires a
    /// Referer that identifies the embedding page; a top-level navigation
    /// straight to /embed/ sends none, and that is what shows "Error 153: video
    /// player configuration error". Serving one small page from a virtual https
    /// host gives the iframe a real origin and a real referrer, while the player
    /// itself is still YouTube's own.
    /// </summary>
    private const string YouTubePlayerHost = "mimi-player.local";

    private const string YouTubePlayerPageName = "player.html";

    private const string YouTubePlayerFolderName = "YouTubePlayerPage";

    private const string YouTubePlayerMarkup = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>Mimi player</title>
        <meta name="referrer" content="strict-origin-when-cross-origin">
        <style>
          /* Fallback only: the host repaints this from the active theme
             as soon as the page finishes navigating. */
          html, body { margin: 0; height: 100%; background: #0b0b10; overflow: hidden; }
          #player, iframe { display: block; border: 0; width: 100%; height: 100%; }
          p { margin: 0; padding: 14px; color: #9aa0b4;
              font: 13px "Segoe UI", system-ui, sans-serif; }
        </style>
        </head>
        <body>
        <p id="fallback" hidden>No YouTube video is selected.</p>
        <div id="player"></div>
        <script>
          (function () {
            var params = new URLSearchParams(window.location.search);
            var videoId = params.get('v') || '';
            // The player keeps its own volume, so it is handed over at start.
            var initialVolume = parseInt(params.get('volume'), 10);
            var player = null;

            // The host drives everything through these two functions with
            // ExecuteScriptAsync: one returns the current state as JSON, the
            // other applies a play/pause/seek command. No message plumbing and
            // no polling inside the page, so there is exactly one path that can
            // break and it is visible in Mimi's log.
            window.mimi = {
              ready: function () { return player !== null; },
              report: function () {
                if (!window.YT || typeof window.YT.Player !== 'function')
                  return { problem: 'api-missing' };
                if (!player || typeof player.getCurrentTime !== 'function')
                  return { problem: 'player-missing' };
                return {
                  state: player.getPlayerState(),
                  position: player.getCurrentTime() || 0,
                  duration: player.getDuration() || 0,
                  // 101/150 mean the owner refused embedding; 153 means
                  // YouTube could not identify this page. Both are worth
                  // reporting instead of showing a progress bar that never
                  // moves.
                  error: window.mimiError || null
                };
              },
              command: function (command) {
                if (!player || !command) return false;
                if (command.command === 'play') player.playVideo();
                else if (command.command === 'pause') player.pauseVideo();
                else if (command.command === 'toggle') {
                  // Read the state here, where it is authoritative, instead of
                  // trusting the host's last report.
                  var state = player.getPlayerState();
                  if (state === 1 || state === 3) player.pauseVideo();
                  else player.playVideo();
                }
                else if (command.command === 'seek') player.seekTo(command.seconds, true);
                else if (command.command === 'volume') {
                  // Volume 0 is Mimi's mute; anything above it unmutes, so the
                  // page and the slider always agree.
                  player.setVolume(command.value);
                  if (command.value > 0) player.unMute();
                }
                else if (command.command === 'restart') {
                  player.seekTo(0, true);
                  player.playVideo();
                }
                return true;
              }
            };

            if (!/^[A-Za-z0-9_-]{11}$/.test(videoId)) {
              document.getElementById('fallback').hidden = false;
              return;
            }

            // The official IFrame Player API is what makes Mimi's transport
            // work: it reports position and state and accepts play/pause/seek.
            window.onYouTubeIframeAPIReady = function () {
              player = new YT.Player('player', {
                videoId: videoId,
                width: '100%',
                height: '100%',
                // origin and widget_referrer identify this page to YouTube.
                // Missing identity is what its player answers with error 153.
                playerVars: {
                  autoplay: 1, rel: 0, playsinline: 1,
                  origin: window.location.origin,
                  widget_referrer: window.location.href
                },
                events: {
                  onReady: function () {
                    if (!isNaN(initialVolume)) player.setVolume(initialVolume);
                    // Scripted play retries autoplay if the browser deferred it.
                    player.playVideo();
                  },
                  onError: function (event) { window.mimiError = event.data; }
                }
              });
            };

            var api = document.createElement('script');
            api.src = 'https://www.youtube.com/iframe_api';
            document.head.appendChild(api);
          }());
        </script>
        </body>
        </html>
        """;

    public MainWindow(MainWindowViewModel viewModel, IAudioPlayer audioPlayer)
    {
        this.viewModel = viewModel;
        this.audioPlayer = audioPlayer;

        InitializeComponent();
        DataContext = viewModel;
        MainVisualizer.FrameProvider = viewModel.VisualizerFrameProvider;

        // Track seeking via the thumb's drag lifecycle (it keeps mouse
        // capture) so releasing outside the slider cannot leave the view
        // model believing the user is still seeking.
        ProgressSlider.AddHandler(
            Thumb.DragStartedEvent,
            new DragStartedEventHandler((_, _) => viewModel.BeginSeek()),
            true);
        ProgressSlider.AddHandler(
            Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, _) => viewModel.EndSeek()),
            true);
        ProgressSlider.LostMouseCapture += (_, _) => viewModel.EndSeek();

        viewModel.StageToggleRequested += ViewModel_StageToggleRequested;
        viewModel.EmbeddedPlayerCommandRequested += OnEmbeddedPlayerCommandRequested;
        viewModel.EmbeddedPlayerDiagnosticRequested += OnEmbeddedPlayerDiagnosticRequested;
        viewModel.YouTubeLinkCopyRequested += OnYouTubeLinkCopyRequested;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // The API key is read from disk, so a user who adds the key file while
        // Mimi is open gets the YouTube entry on the next window activation
        // instead of having to restart.
        Activated += (_, _) => viewModel.RefreshYouTubeAvailability();
        metricsTimer.Tick += MetricsTimer_Tick;
        metricsTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        mainWindowHandle = new WindowInteropHelper(this).Handle;

        // With an explicit AppUserModelID, the taskbar group icon is a
        // shell identity concern rather than just the WPF window icon.
        // Publish the relaunch icon on this HWND before the window is
        // shown so the shell does not keep resolving the old shortcut icon.
        WindowsAppIdentity.TrySetWindowAppUserModelProperties(mainWindowHandle);
        WindowChromeHelper.ApplyDarkTitleBar(this);

        // SMTC is keyed to the top-level HWND. It must therefore be created
        // after WPF has created the native window, not in the view model.
        mediaControls = new WindowsMediaControlsService(
            viewModel,
            audioPlayer,
            Dispatcher);
        mediaControls.Initialize(mainWindowHandle);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (mainWindowHandle != IntPtr.Zero)
        {
            WindowsAppIdentity.TryClearWindowAppUserModelProperties(
                mainWindowHandle);
            mainWindowHandle = IntPtr.Zero;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        metricsTimer.Stop();
        metricsTimer.Tick -= MetricsTimer_Tick;
        viewModel.StageToggleRequested -= ViewModel_StageToggleRequested;
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        try
        {
            YouTubeBrowser.Dispose();
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(
                "Could not dispose the YouTube player: {ExceptionType}",
                ex.GetType().FullName);
        }

        stageWindow?.Close();
        mediaControls?.Dispose();
        mediaControls = null;
        viewModel.Dispose();
        base.OnClosed(e);
    }

    private void MetricsTimer_Tick(object? sender, EventArgs e)
    {
        viewModel.UpdateVisualizerMetrics(
            MainVisualizer.FramesPerSecond,
            MainVisualizer.AverageDrawMilliseconds);

        // The video's timeline lives in the page, so it is sampled here rather
        // than from the audio pipeline.
        if (viewModel.IsYouTubePlaying)
            _ = PollEmbeddedPlayerAsync();
    }

    private void ViewModel_StageToggleRequested(object? sender, EventArgs e) =>
        ToggleStage();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.UiTheme))
            WindowChromeHelper.ApplyDarkTitleBar(this);

        // The embedded YouTube player is driven from here: it must react even
        // when the Stage window is closed, so this hook runs before the
        // Stage-specific early return.
        if (e.PropertyName == nameof(MainWindowViewModel.IsYouTubePlaying))
        {
            _ = SyncYouTubePlayerAsync();

            // Closing the player leaves nothing to stage.
            if (!viewModel.IsYouTubePlaying)
                ExitVideoStage();
        }

        if (stageWindow is null) return;

        if (e.PropertyName == nameof(MainWindowViewModel.VisualizerMode))
        {
            stageWindow.SetMode(viewModel.VisualizerMode);
        }
        else if (e.PropertyName is
                 nameof(MainWindowViewModel.NowPlayingTitle) or
                 nameof(MainWindowViewModel.NowPlayingSubtitle))
        {
            stageWindow.SetTrack(viewModel.NowPlayingTitle, viewModel.NowPlayingSubtitle);
        }
        else if (e.PropertyName is
                 nameof(MainWindowViewModel.CurrentLyricLine) or
                 nameof(MainWindowViewModel.PreviousLyricLine) or
                 nameof(MainWindowViewModel.NextLyricLine))
        {
            PushStageLyrics();
        }
    }

    /// <summary>
    /// Drives the single embedded player. Mimi only navigates YouTube's own
    /// player: no stream is resolved and no media is downloaded. Hiding the
    /// pane navigates the browser away, which stops playback, so YouTube audio
    /// can never keep running without a visible player.
    /// </summary>
    private async Task SyncYouTubePlayerAsync()
    {
        try
        {
            if (!viewModel.IsYouTubePlaying)
            {
                if (youTubeBrowserReady)
                    YouTubeBrowser.CoreWebView2.Navigate("about:blank");

                return;
            }

            // The property change that started this runs before layout, so the
            // player pane can still be collapsed here. WebView2 refuses to
            // initialise outside the visual tree, so let layout catch up first.
            for (var attempt = 0; attempt < 10 && !YouTubeBrowser.IsVisible; attempt++)
                await Dispatcher.Yield(DispatcherPriority.Loaded);

            if (!youTubeBrowserReady)
            {
                // A dedicated profile keeps the user's own YouTube session
                // (sign-in, Premium) between Mimi runs, like a browser profile.
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Mimi",
                    "YouTubeBrowserProfile");
                Directory.CreateDirectory(userDataFolder);

                // The click that started this happened in WPF, not in the page,
                // so Chromium would otherwise treat playback as gesture-less
                // and refuse to start it with sound.
                var options = new CoreWebView2EnvironmentOptions(
                    "--autoplay-policy=no-user-gesture-required");

                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder,
                    options: options);
                await YouTubeBrowser.EnsureCoreWebView2Async(environment);

                var initialized = YouTubeBrowser.CoreWebView2;
                if (initialized is null) return;

                // A folder of its own, next to (not inside) the Chromium
                // profile: the profile is browser state, this is one static
                // page that Mimi regenerates on every start.
                var pageFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Mimi",
                    YouTubePlayerFolderName);
                Directory.CreateDirectory(pageFolder);
                File.WriteAllText(
                    Path.Combine(pageFolder, YouTubePlayerPageName),
                    YouTubePlayerMarkup);

                initialized.SetVirtualHostNameToFolderMapping(
                    YouTubePlayerHost,
                    pageFolder,
                    CoreWebView2HostResourceAccessKind.DenyCors);

                // YouTube requires the request to identify the embedding page
                // through Referer. Chromium normally sends it for an iframe,
                // but WebView2 strips it in a few cases (virtual hosts
                // included), which is exactly the "Error 153: video player
                // configuration error" the owner hit. Only a missing header is
                // filled in; anything the browser already sent is left alone.
                initialized.AddWebResourceRequestedFilter(
                    "https://www.youtube.com/*",
                    CoreWebView2WebResourceContext.All);
                initialized.AddWebResourceRequestedFilter(
                    "https://*.youtube.com/*",
                    CoreWebView2WebResourceContext.All);
                initialized.WebResourceRequested += OnYouTubeResourceRequested;

                // A navigation aborts any script call in progress, so the
                // in-flight flag must not survive one. The same event paints
                // the page with the active theme.
                initialized.NavigationCompleted += (_, _) =>
                {
                    youTubePollInFlight = false;
                    _ = ApplyPlayerBackdropAsync();
                };

                youTubeBrowserReady = true;
            }

            var core = YouTubeBrowser.CoreWebView2;
            if (core is null) return;

            var video = viewModel.CurrentYouTubeVideo?.Video;
            if (video is null) return;

            embeddedPlayerSilences = 0;
            reportedEmbeddedPlayerError = 0;

            // The page applies this volume when its player becomes ready, so
            // the first second of sound matches Mimi's slider.
            core.Navigate(
                $"https://{YouTubePlayerHost}/{YouTubePlayerPageName}" +
                $"?v={Uri.EscapeDataString(video.VideoId)}" +
                $"&volume={viewModel.YouTubeVolumePercent}");
        }
        catch (Exception ex)
        {
            // A missing WebView2 Runtime must not break the rest of Mimi.
            Serilog.Log.Warning(
                "Could not start the YouTube player: {ExceptionType}",
                ex.GetType().FullName);
        }
    }

    private void ToggleStage()
    {
        // A playing video is staged in this window (see MainWindow.VideoStage):
        // the player cannot follow the user to a second window.
        if (isVideoStageOpen || viewModel.IsYouTubePlaying)
        {
            ToggleVideoStage();
            return;
        }

        if (stageWindow is { IsLoaded: true })
        {
            stageWindow.Close();
            return;
        }

        OpenStage();
    }

    private void OpenStage()
    {
        if (stageWindow is { IsLoaded: true })
        {
            stageWindow.Activate();
            return;
        }

        stageWindow = new StageWindow(
            audioPlayer,
            viewModel.VisualizerMode,
            viewModel.Loc)
        {
            Owner = this
        };
        stageWindow.SetTrack(viewModel.NowPlayingTitle, viewModel.NowPlayingSubtitle);
        PushStageLyrics();
        stageWindow.ModeChanged += StageWindow_ModeChanged;
        stageWindow.Closed += StageWindow_Closed;
        stageWindow.Show();
    }

    /// <summary>
    /// Sends a transport request to the player page. Nothing is sent before the
    /// page exists, so an early click cannot be lost in a null browser.
    /// </summary>
    private void OnEmbeddedPlayerCommandRequested(EmbeddedPlayerCommand command)
    {
        if (!youTubeBrowserReady) return;

        var payload = command.Kind switch
        {
            EmbeddedPlayerCommandKind.Play => "{\"command\":\"play\"}",
            EmbeddedPlayerCommandKind.Pause => "{\"command\":\"pause\"}",
            EmbeddedPlayerCommandKind.Toggle => "{\"command\":\"toggle\"}",
            EmbeddedPlayerCommandKind.Restart => "{\"command\":\"restart\"}",
            EmbeddedPlayerCommandKind.SetVolume => JsonSerializer.Serialize(new
            {
                command = "volume",
                value = (int)Math.Clamp(Math.Round(command.Seconds), 0, 100)
            }),
            _ => JsonSerializer.Serialize(new
            {
                command = "seek",
                seconds = Math.Max(0, command.Seconds)
            })
        };

        _ = ExecuteInPlayerAsync($"window.mimi && window.mimi.command({payload})");
    }

    /// <summary>
    /// Reads the player's own position and state. Called from the same timer
    /// that refreshes the visualizer, because the video timeline belongs to the
    /// page and Mimi has nothing else to sample.
    /// </summary>
    private async Task PollEmbeddedPlayerAsync()
    {
        if (!youTubeBrowserReady || youTubePollInFlight) return;

        var core = YouTubeBrowser.CoreWebView2;
        if (core is null) return;

        youTubePollInFlight = true;
        try
        {
            // Returning the object itself means the result is already JSON;
            // no double parsing and no message channel involved.
            var script = core.ExecuteScriptAsync(
                "window.mimi && window.mimi.report ? window.mimi.report() : null");

            // A script call issued while the page is navigating can stay
            // pending forever. Without this watchdog the in-flight flag would
            // stay set and Mimi would never read the player again: the video
            // plays, but the Play button and the timeline look dead.
            var completed = await Task.WhenAny(
                script,
                Task.Delay(TimeSpan.FromSeconds(2.5)));

            if (!ReferenceEquals(completed, script))
            {
                Serilog.Log.Debug("The YouTube player did not answer a poll in time.");
                return;
            }

            var result = await script;

            if (result is null or "null" or "undefined" or "")
            {
                NoteEmbeddedPlayerSilence();
                return;
            }

            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            if (root.TryGetProperty("problem", out var problem) &&
                problem.ValueKind == JsonValueKind.String)
            {
                Serilog.Log.Debug(
                    "The YouTube page has no player yet: {Problem}",
                    problem.GetString());
                NoteEmbeddedPlayerSilence();
                return;
            }

            if (!root.TryGetProperty("state", out var state) ||
                !root.TryGetProperty("position", out var position) ||
                !root.TryGetProperty("duration", out var duration))
            {
                return;
            }

            // The page records the last player error. Reporting it once keeps
            // "nothing happens when I press play" from being a silent failure.
            if (root.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Number)
            {
                var code = error.GetInt32();
                if (code != reportedEmbeddedPlayerError)
                {
                    reportedEmbeddedPlayerError = code;
                    viewModel.ReportEmbeddedPlayerError(code);
                }
            }
            else
            {
                reportedEmbeddedPlayerError = 0;
            }

            embeddedPlayerSilences = 0;
            viewModel.ApplyEmbeddedPlayerState(
                state.GetInt32(),
                position.GetDouble(),
                duration.GetDouble());
        }
        catch (Exception ex)
        {
            // Not loaded yet, the JSON shape changed, the page is mid-navigation
            // or the browser went away with the window. Whatever it is, one
            // failed poll must never stop the next one.
            Serilog.Log.Debug(
                "The YouTube player poll failed: {ExceptionType}",
                ex.GetType().FullName);
            NoteEmbeddedPlayerSilence();
        }
        finally
        {
            youTubePollInFlight = false;
        }
    }

    /// <summary>
    /// Three silent polls in a row means the page never created a player. That
    /// is worth saying out loud instead of leaving a dead progress bar.
    /// </summary>
    /// <summary>
    /// Paints the page that hosts the official player with the active theme.
    /// The video belongs to YouTube, but the surface around it is Mimi: a fixed
    /// near-black rectangle would be the one place every theme stops short.
    /// </summary>
    private async Task ApplyPlayerBackdropAsync()
    {
        var core = YouTubeBrowser.CoreWebView2;
        if (core is null) return;

        if (TryFindResource("AppBackgroundColor") is not System.Windows.Media.Color backdrop)
            return;

        var hex = "#"
            + backdrop.R.ToString("X2")
            + backdrop.G.ToString("X2")
            + backdrop.B.ToString("X2");

        try
        {
            await core.ExecuteScriptAsync(
                "document.documentElement.style.background='"
                + hex
                + "';if(document.body){document.body.style.background='"
                + hex
                + "';}");
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(
                "The player backdrop could not be themed: {Error}",
                ex.GetType().FullName);
        }
    }

    private void NoteEmbeddedPlayerSilence()
    {
        embeddedPlayerSilences++;

        if (embeddedPlayerSilences != 3) return;

        Serilog.Log.Warning(
            "The embedded YouTube player reported no state after three polls " +
            "(browser ready: {BrowserReady})",
            youTubeBrowserReady);

        viewModel.ReportEmbeddedPlayerUnavailable();

        // Ask the page what it sees and show it: a screenshot of the panel is
        // then enough to tell a missing bridge from a slow page.
        _ = RunEmbeddedPlayerDiagnosticAsync();
    }

    /// <summary>
    /// Fills in a missing Referer for YouTube's own requests. YouTube rejects
    /// an embed it cannot attribute (error 153), and WebView2 does not always
    /// send the header itself. A header the browser already set is kept as is.
    /// </summary>
    private void OnYouTubeResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs args)
    {
        try
        {
            if (args.Request.Headers.Contains("Referer")) return;

            args.Request.Headers.SetHeader(
                "Referer",
                $"https://{YouTubePlayerHost}/{YouTubePlayerPageName}");
        }
        catch (ArgumentException ex)
        {
            // An immutable or malformed header set is not worth failing a
            // navigation over; the player reports the outcome either way.
            Serilog.Log.Debug(
                "Could not set the YouTube referrer: {ExceptionType}",
                ex.GetType().FullName);
        }
    }

    private void OnYouTubeLinkCopyRequested(string link)
    {
        try
        {
            System.Windows.Clipboard.SetText(link);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException)
        {
            // Another process can hold the clipboard; the status line already
            // reported the copy, so only the log records the failure.
            Serilog.Log.Debug(
                "Could not put the YouTube link on the clipboard: {ExceptionType}",
                ex.GetType().FullName);
        }
    }

    /// <summary>
    /// One click, one straight answer about the bridge: is `window.mimi` there,
    /// did the player get created, what does it report, and did YouTube send an
    /// error code. Shown in the panel so it can be read without opening a log.
    /// </summary>
    private void OnEmbeddedPlayerDiagnosticRequested()
    {
        _ = RunEmbeddedPlayerDiagnosticAsync();
    }

    private async Task RunEmbeddedPlayerDiagnosticAsync()
    {
        const string script = """
            (function () {
              try {
                if (!window.mimi) return 'no bridge';
                var report = window.mimi.report ? window.mimi.report() : null;
                return JSON.stringify({
                  ready: window.mimi.ready ? window.mimi.ready() : false,
                  error: window.mimiError || null,
                  state: report ? report.state : null,
                  duration: report ? report.duration : null,
                  position: report ? report.position : null
                });
              } catch (e) {
                return 'script error: ' + (e && e.message ? e.message : 'unknown');
              }
            }())
            """;

        var core = YouTubeBrowser.CoreWebView2;
        if (!youTubeBrowserReady || core is null)
        {
            viewModel.ApplyEmbeddedPlayerDiagnostic("browser not ready");
            return;
        }

        try
        {
            var raw = await core.ExecuteScriptAsync(script);
            // A script that returns a string comes back JSON-quoted.
            var text = JsonSerializer.Deserialize<string>(raw) ?? raw;
            viewModel.ApplyEmbeddedPlayerDiagnostic(text);
        }
        catch (Exception ex) when (
            ex is JsonException or InvalidOperationException or ObjectDisposedException)
        {
            viewModel.ApplyEmbeddedPlayerDiagnostic(
                ex.GetType().Name);
        }
    }

    private async Task ExecuteInPlayerAsync(string script)
    {
        var core = YouTubeBrowser.CoreWebView2;
        if (core is null) return;

        try
        {
            await core.ExecuteScriptAsync(script);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            Serilog.Log.Debug(
                "Could not send a command to the YouTube player: {ExceptionType}",
                ex.GetType().FullName);
        }
    }

    private void PushStageLyrics() =>
        stageWindow?.SetLyrics(
            viewModel.PreviousLyricLine?.Text,
            viewModel.CurrentLyricLine?.Text,
            viewModel.NextLyricLine?.Text);

    private void StageWindow_ModeChanged(VisualizerDisplayMode mode) =>
        viewModel.VisualizerMode = mode;

    private void StageWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is StageWindow stage)
        {
            stage.ModeChanged -= StageWindow_ModeChanged;
            stage.Closed -= StageWindow_Closed;
        }

        stageWindow = null;
    }
}
