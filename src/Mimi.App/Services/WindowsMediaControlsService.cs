using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using Mimi.Core.Playback;
using Mimi.ViewModels;
using Serilog;
using Windows.Media;
using Windows.Storage.Streams;

namespace Mimi.Services;

/// <summary>
/// Bridges Mimi's existing view-model playback state to Windows System
/// Media Transport Controls (the media flyout, lock screen, headsets, and
/// hardware media keys). This class deliberately does not own playback
/// order: queue, shuffle, and repeat decisions remain in the view model and
/// Core queue. SMTC failures are logged and never stop local playback.
/// </summary>
public sealed class WindowsMediaControlsService : IDisposable
{
    private const long MaximumThumbnailBytes = 10 * 1024 * 1024;

    private readonly MainWindowViewModel viewModel;
    private readonly IAudioPlayer audioPlayer;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timelineTimer;

    private SystemMediaTransportControls? controls;
    private TrackItemViewModel? observedTrack;
    private IRandomAccessStream? thumbnailStream;
    private CancellationTokenSource? thumbnailCancellation;
    private int thumbnailVersion;
    private bool failureLogged;
    private bool disposed;

    public WindowsMediaControlsService(
        MainWindowViewModel viewModel,
        IAudioPlayer audioPlayer,
        Dispatcher dispatcher)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        timelineTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timelineTimer.Tick += TimelineTimer_Tick;
    }

    public bool IsAvailable => controls is not null;

    public void Initialize(IntPtr windowHandle)
    {
        if (disposed || controls is not null || windowHandle == IntPtr.Zero)
            return;

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            Log.Information(
                "Windows media controls are unavailable before Windows 10 1809");
            return;
        }

        try
        {
            var newControls =
                SystemMediaTransportControlsInterop.GetForWindow(windowHandle);
            newControls.ButtonPressed += Controls_ButtonPressed;
            newControls.PlaybackPositionChangeRequested +=
                Controls_PlaybackPositionChangeRequested;
            newControls.AutoRepeatModeChangeRequested +=
                Controls_AutoRepeatModeChangeRequested;
            newControls.ShuffleEnabledChangeRequested +=
                Controls_ShuffleEnabledChangeRequested;

            controls = newControls;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            SetObservedTrack(viewModel.CurrentTrack);
            SyncAll();
            timelineTimer.Start();

            Log.Information("Windows media controls initialized");
        }
        catch (Exception ex)
        {
            failureLogged = true;
            controls = null;
            Log.Warning(ex, "Could not initialize Windows media controls");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        timelineTimer.Stop();
        timelineTimer.Tick -= TimelineTimer_Tick;
        thumbnailCancellation?.Cancel();
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        SetObservedTrack(null);

        if (controls is not null)
        {
            var controlsToClose = controls;
            TrySmtc(
                () =>
                {
                    controlsToClose.ButtonPressed -= Controls_ButtonPressed;
                    controlsToClose.PlaybackPositionChangeRequested -=
                        Controls_PlaybackPositionChangeRequested;
                    controlsToClose.AutoRepeatModeChangeRequested -=
                        Controls_AutoRepeatModeChangeRequested;
                    controlsToClose.ShuffleEnabledChangeRequested -=
                        Controls_ShuffleEnabledChangeRequested;
                    controlsToClose.DisplayUpdater.ClearAll();
                    controlsToClose.DisplayUpdater.Update();
                    controlsToClose.PlaybackStatus = MediaPlaybackStatus.Closed;
                    controlsToClose.IsEnabled = false;
                },
                "close Windows media controls");
            controls = null;
        }

        thumbnailStream?.Dispose();
        thumbnailStream = null;

        // Cancellation was requested above. The in-flight update (if any)
        // owns and disposes its CancellationTokenSource in its finally
        // block, so only detach the field here.
        thumbnailCancellation = null;
    }

    private void TimelineTimer_Tick(object? sender, EventArgs e)
    {
        SyncPlaybackState();
        SyncTimeline();
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (disposed || controls is null) return;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(
                new Action(() => ViewModel_PropertyChanged(sender, e)));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.CurrentTrack):
                SetObservedTrack(viewModel.CurrentTrack);
                SyncAll();
                break;
            case nameof(MainWindowViewModel.CurrentCoverArtPath):
                QueueThumbnailUpdate();
                break;
            case nameof(MainWindowViewModel.IsPlaying):
                SyncPlaybackState();
                SyncTimeline();
                break;
            case nameof(MainWindowViewModel.IsShuffleEnabled):
            case nameof(MainWindowViewModel.RepeatMode):
                SyncPlaybackModes();
                break;
        }
    }

    private void ObservedTrack_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (disposed || controls is null) return;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(
                new Action(() => ObservedTrack_PropertyChanged(sender, e)));
            return;
        }

        if (e.PropertyName == nameof(TrackItemViewModel.CoverArtPath))
        {
            QueueThumbnailUpdate();
            return;
        }

        if (e.PropertyName is
            nameof(TrackItemViewModel.Title) or
            nameof(TrackItemViewModel.Artist) or
            nameof(TrackItemViewModel.Album) or
            nameof(TrackItemViewModel.AlbumArtist) or
            nameof(TrackItemViewModel.Genre))
        {
            SyncDisplay(refreshThumbnail: false);
        }
    }

    private void SetObservedTrack(TrackItemViewModel? track)
    {
        if (ReferenceEquals(observedTrack, track)) return;

        if (observedTrack is not null)
            observedTrack.PropertyChanged -= ObservedTrack_PropertyChanged;

        observedTrack = track;

        if (observedTrack is not null)
            observedTrack.PropertyChanged += ObservedTrack_PropertyChanged;
    }

    private void SyncAll()
    {
        SyncPlaybackState();
        SyncDisplay(refreshThumbnail: true);
        SyncPlaybackModes();
        SyncTimeline();
    }

    private void SyncDisplay(bool refreshThumbnail)
    {
        if (disposed || controls is null) return;

        var track = viewModel.CurrentTrack;
        if (track is null)
        {
            CancelThumbnailUpdate();
            TrySmtc(
                () =>
                {
                    controls.DisplayUpdater.ClearAll();
                    controls.DisplayUpdater.Update();
                },
                "clear Windows media metadata");
            return;
        }

        TrySmtc(
            () =>
            {
                var updater = controls.DisplayUpdater;
                updater.AppMediaId = "Mimi";
                updater.Type = MediaPlaybackType.Music;

                var music = updater.MusicProperties;
                music.Title = track.Title;
                music.Artist = track.Track.Artist;
                music.AlbumArtist = track.Track.AlbumArtist;
                music.AlbumTitle = track.Track.Album;
                music.Genres.Clear();
                if (!string.IsNullOrWhiteSpace(track.Track.Genre))
                    music.Genres.Add(track.Track.Genre);

                if (refreshThumbnail)
                    updater.Thumbnail = null;

                updater.Update();
            },
            "update Windows media metadata");

        if (refreshThumbnail)
            QueueThumbnailUpdate();
    }

    private void SyncPlaybackState()
    {
        if (disposed || controls is null) return;

        var hasTrack = viewModel.CurrentTrack is not null;
        var canSeek = hasTrack && audioPlayer.Duration > TimeSpan.Zero;
        var status = !hasTrack
            ? MediaPlaybackStatus.Closed
            : viewModel.IsPlaying
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;

        TrySmtc(
            () =>
            {
                if (controls.IsEnabled != hasTrack)
                    controls.IsEnabled = hasTrack;
                if (controls.PlaybackStatus != status)
                    controls.PlaybackStatus = status;
                if (controls.IsPlayEnabled != hasTrack)
                    controls.IsPlayEnabled = hasTrack;
                if (controls.IsPauseEnabled != (hasTrack && viewModel.IsPlaying))
                    controls.IsPauseEnabled = hasTrack && viewModel.IsPlaying;
                if (controls.IsNextEnabled != hasTrack)
                    controls.IsNextEnabled = hasTrack;
                if (controls.IsPreviousEnabled != hasTrack)
                    controls.IsPreviousEnabled = hasTrack;
                if (controls.IsFastForwardEnabled != canSeek)
                    controls.IsFastForwardEnabled = canSeek;
                if (controls.IsRewindEnabled != canSeek)
                    controls.IsRewindEnabled = canSeek;
                if (controls.IsStopEnabled)
                    controls.IsStopEnabled = false;
            },
            "update Windows media playback state");
    }

    private void SyncPlaybackModes()
    {
        if (disposed || controls is null) return;

        var repeatMode = viewModel.RepeatMode switch
        {
            RepeatMode.All => MediaPlaybackAutoRepeatMode.List,
            RepeatMode.One => MediaPlaybackAutoRepeatMode.Track,
            _ => MediaPlaybackAutoRepeatMode.None
        };

        TrySmtc(
            () =>
            {
                if (controls.ShuffleEnabled != viewModel.IsShuffleEnabled)
                    controls.ShuffleEnabled = viewModel.IsShuffleEnabled;
                if (controls.AutoRepeatMode != repeatMode)
                    controls.AutoRepeatMode = repeatMode;
            },
            "update Windows shuffle/repeat state");
    }

    private void SyncTimeline()
    {
        if (disposed || controls is null || viewModel.CurrentTrack is null)
            return;

        var duration = audioPlayer.Duration;
        if (duration <= TimeSpan.Zero &&
            viewModel.CurrentTrack.Track.Duration is { } metadataDuration)
        {
            duration = metadataDuration;
        }

        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

        var position = audioPlayer.CurrentTime;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (duration > TimeSpan.Zero && position > duration)
            position = duration;

        TrySmtc(
            () => controls.UpdateTimelineProperties(
                new SystemMediaTransportControlsTimelineProperties
                {
                    StartTime = TimeSpan.Zero,
                    MinSeekTime = TimeSpan.Zero,
                    Position = position,
                    MaxSeekTime = duration,
                    EndTime = duration
                }),
            "update Windows media timeline");
    }

    private void QueueThumbnailUpdate()
    {
        if (disposed || controls is null) return;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(QueueThumbnailUpdate));
            return;
        }

        var version = ++thumbnailVersion;
        var previousCancellation = thumbnailCancellation;
        var cancellationSource = new CancellationTokenSource();
        thumbnailCancellation = cancellationSource;

        // The superseded task still owns its source and disposes it from
        // its own finally block. Cancelling here only asks that task to
        // stop; disposing the source here could race token callbacks that
        // are still unwinding.
        previousCancellation?.Cancel();
        _ = UpdateThumbnailAsync(version, cancellationSource);
    }

    private void CancelThumbnailUpdate()
    {
        thumbnailVersion++;
        thumbnailCancellation?.Cancel();
        thumbnailStream?.Dispose();
        thumbnailStream = null;
    }

    private async Task UpdateThumbnailAsync(
        int version,
        CancellationTokenSource cancellationSource)
    {
        var cancellationToken = cancellationSource.Token;
        IRandomAccessStream? newStream = null;

        try
        {
            var path = viewModel.CurrentCoverArtPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length is > 0 and <= MaximumThumbnailBytes)
                {
                    var bytes = await File.ReadAllBytesAsync(
                        path,
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    newStream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(newStream))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync().AsTask(cancellationToken);
                        writer.DetachStream();
                    }

                    newStream.Seek(0);
                }
            }

            if (disposed || controls is null || version != thumbnailVersion)
            {
                newStream?.Dispose();
                return;
            }

            controls.DisplayUpdater.Thumbnail = newStream is null
                ? null
                : RandomAccessStreamReference.CreateFromStream(newStream);
            controls.DisplayUpdater.Update();

            var oldStream = thumbnailStream;
            thumbnailStream = newStream;
            newStream = null;
            oldStream?.Dispose();
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            newStream?.Dispose();
        }
        catch (Exception ex)
        {
            newStream?.Dispose();
            LogFailure(ex, "update Windows media thumbnail");
        }
        finally
        {
            // Each update owns exactly one source. A superseded task can
            // safely dispose its own source here; the newest task also
            // clears the field so completed sources do not accumulate.
            if (ReferenceEquals(thumbnailCancellation, cancellationSource))
                thumbnailCancellation = null;

            cancellationSource.Dispose();
        }
    }

    private void Controls_ButtonPressed(
        SystemMediaTransportControls sender,
        SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        var button = args.Button;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;

            switch (button)
            {
                case SystemMediaTransportControlsButton.Play:
                    viewModel.ResumePlaybackCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    viewModel.PausePlaybackCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Next:
                    viewModel.NextCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    viewModel.PreviousCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.FastForward:
                    viewModel.SeekForward10Command.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Rewind:
                    viewModel.SeekBackward10Command.Execute(null);
                    break;
            }

            SyncPlaybackState();
            SyncTimeline();
        }));
    }

    private void Controls_PlaybackPositionChangeRequested(
        SystemMediaTransportControls sender,
        PlaybackPositionChangeRequestedEventArgs args)
    {
        var position = args.RequestedPlaybackPosition;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;

            viewModel.SeekToPosition(position);
            SyncTimeline();
        }));
    }

    private void Controls_AutoRepeatModeChangeRequested(
        SystemMediaTransportControls sender,
        AutoRepeatModeChangeRequestedEventArgs args)
    {
        var requested = args.RequestedAutoRepeatMode;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;

            viewModel.RepeatMode = requested switch
            {
                MediaPlaybackAutoRepeatMode.List => RepeatMode.All,
                MediaPlaybackAutoRepeatMode.Track => RepeatMode.One,
                _ => RepeatMode.Off
            };
            SyncPlaybackModes();
        }));
    }

    private void Controls_ShuffleEnabledChangeRequested(
        SystemMediaTransportControls sender,
        ShuffleEnabledChangeRequestedEventArgs args)
    {
        var requested = args.RequestedShuffleEnabled;
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;

            viewModel.IsShuffleEnabled = requested;
            SyncPlaybackModes();
        }));
    }

    private void TrySmtc(Action action, string operation)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogFailure(ex, operation);
        }
    }

    private void LogFailure(Exception exception, string operation)
    {
        // Timeline/state synchronization runs every second. One warning is
        // enough to diagnose a broken SMTC integration without filling the
        // rolling log with the same failure.
        if (failureLogged) return;

        failureLogged = true;
        Log.Warning(exception, "Could not {Operation}", operation);
    }
}
