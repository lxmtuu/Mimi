using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mimi.Lyrics;
using Mimi.Models;
using Mimi.Services;
using Serilog;

namespace Mimi.ViewModels;

/// <summary>
/// Offline lyrics presentation for the main window. Lyrics are loaded by an
/// injected service whenever the adopted current track changes; playback
/// itself never waits for or depends on that best-effort work.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private const int LyricsOffsetStepMilliseconds = 100;

    private readonly ILyricsService? lyricsService;
    private CancellationTokenSource? lyricsLoadCancellation;
    private LyricsDocument? currentLyricsDocument;
    private LyricsPresentationState lyricsPresentationState =
        LyricsPresentationState.NoTrack;
    private int lyricsLoadVersion;
    private int currentLyricLineIndex = -1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadLyricsCommand))]
    private bool isLyricsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjustLyricsOffset))]
    [NotifyPropertyChangedFor(nameof(CanResetLyricsOffset))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseLyricsOffsetCommand))]
    [NotifyCanExecuteChangedFor(nameof(IncreaseLyricsOffsetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetLyricsOffsetCommand))]
    private bool hasLyrics;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdjustLyricsOffset))]
    [NotifyPropertyChangedFor(nameof(CanResetLyricsOffset))]
    [NotifyCanExecuteChangedFor(nameof(DecreaseLyricsOffsetCommand))]
    [NotifyCanExecuteChangedFor(nameof(IncreaseLyricsOffsetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetLyricsOffsetCommand))]
    private bool isLyricsSynchronized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LyricsOffsetLabel))]
    [NotifyPropertyChangedFor(nameof(CanResetLyricsOffset))]
    [NotifyCanExecuteChangedFor(nameof(ResetLyricsOffsetCommand))]
    private int lyricsOffsetMilliseconds;

    [ObservableProperty]
    private LyricLineViewModel? currentLyricLine;

    public ObservableCollection<LyricLineViewModel> LyricLines { get; } = [];

    public bool CanAdjustLyricsOffset =>
        HasLyrics && IsLyricsSynchronized;

    public bool CanResetLyricsOffset =>
        CanAdjustLyricsOffset && LyricsOffsetMilliseconds != 0;

    public bool CanReloadLyrics =>
        CurrentTrack is not null &&
        lyricsService is not null &&
        !IsLyricsLoading;

    public string LyricsOffsetLabel => string.Format(
        CultureInfo.InvariantCulture,
        "{0:+0.0;-0.0;0.0}s",
        LyricsOffsetMilliseconds / 1000d);

    public string LyricsStatusLabel => lyricsPresentationState switch
    {
        LyricsPresentationState.NoTrack => Loc["Lyrics_NoTrack"],
        LyricsPresentationState.Loading => Loc["Lyrics_Loading"],
        LyricsPresentationState.NotFound => Loc["Lyrics_NotFound"],
        LyricsPresentationState.Unavailable => Loc["Lyrics_Unavailable"],
        LyricsPresentationState.LoadFailed => Loc["Lyrics_LoadFailed"],
        LyricsPresentationState.Ready when !IsLyricsSynchronized =>
            Loc["Lyrics_Unsynchronized"],
        _ => string.Empty
    };

    public string LyricsSourceLabel => currentLyricsDocument?.Source switch
    {
        LyricsSourceKind.SidecarFile => Loc["Lyrics_SourceSidecar"],
        LyricsSourceKind.EmbeddedTag => Loc["Lyrics_SourceEmbedded"],
        _ => string.Empty
    };

    [RelayCommand(CanExecute = nameof(CanAdjustLyricsOffset))]
    private void DecreaseLyricsOffset() =>
        AdjustLyricsOffset(-LyricsOffsetStepMilliseconds);

    [RelayCommand(CanExecute = nameof(CanAdjustLyricsOffset))]
    private void IncreaseLyricsOffset() =>
        AdjustLyricsOffset(LyricsOffsetStepMilliseconds);

    [RelayCommand(CanExecute = nameof(CanResetLyricsOffset))]
    private void ResetLyricsOffset() => SetLyricsOffset(0);

    [RelayCommand(CanExecute = nameof(CanReloadLyrics))]
    private void ReloadLyrics()
    {
        if (CurrentTrack is { } track)
            StartLyricsLoad(track);
    }

    private void StartLyricsLoad(TrackItemViewModel? track)
    {
        lyricsLoadCancellation?.Cancel();
        var loadVersion = ++lyricsLoadVersion;

        currentLyricsDocument = null;
        currentLyricLineIndex = -1;
        CurrentLyricLine = null;
        LyricLines.Clear();
        HasLyrics = false;
        IsLyricsSynchronized = false;
        LyricsOffsetMilliseconds = track is null
            ? 0
            : GetSavedLyricsOffset(track.FilePath);
        IsLyricsLoading = false;

        if (track is null)
        {
            SetLyricsPresentationState(LyricsPresentationState.NoTrack);
            NotifyLyricsCommandsCanExecuteChanged();
            return;
        }

        if (lyricsService is null)
        {
            SetLyricsPresentationState(LyricsPresentationState.Unavailable);
            NotifyLyricsCommandsCanExecuteChanged();
            return;
        }

        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token);
        lyricsLoadCancellation = loadCancellation;
        IsLyricsLoading = true;
        SetLyricsPresentationState(LyricsPresentationState.Loading);
        NotifyLyricsCommandsCanExecuteChanged();

        _ = LoadLyricsAsync(track, loadVersion, loadCancellation);
    }

    private async Task LoadLyricsAsync(
        TrackItemViewModel track,
        int loadVersion,
        CancellationTokenSource loadCancellation)
    {
        try
        {
            var document = await lyricsService!.LoadAsync(
                track.Track,
                loadCancellation.Token);
            if (!IsCurrentLyricsLoad(loadVersion, track)) return;

            if (document is null || document.Lines.Count == 0)
            {
                SetLyricsPresentationState(LyricsPresentationState.NotFound);
                return;
            }

            currentLyricsDocument = document;
            foreach (var line in document.Lines)
                LyricLines.Add(new LyricLineViewModel(line));

            HasLyrics = true;
            IsLyricsSynchronized = document.IsSynchronized;
            SetLyricsPresentationState(LyricsPresentationState.Ready);
            UpdateCurrentLyricLine(audioPlayer.CurrentTime, force: true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(
                ex,
                "Could not load lyrics for {FilePath}",
                track.FilePath);
            if (IsCurrentLyricsLoad(loadVersion, track))
                SetLyricsPresentationState(LyricsPresentationState.LoadFailed);
        }
        finally
        {
            if (IsCurrentLyricsLoad(loadVersion, track))
            {
                IsLyricsLoading = false;
                NotifyLyricsCommandsCanExecuteChanged();
            }

            if (ReferenceEquals(lyricsLoadCancellation, loadCancellation))
                lyricsLoadCancellation = null;

            loadCancellation.Dispose();
        }
    }

    private bool IsCurrentLyricsLoad(
        int loadVersion,
        TrackItemViewModel track) =>
        !disposed &&
        loadVersion == lyricsLoadVersion &&
        ReferenceEquals(CurrentTrack, track);

    private void CancelLyricsLoad()
    {
        lyricsLoadVersion++;
        lyricsLoadCancellation?.Cancel();
    }

    private void UpdateCurrentLyricLine(
        TimeSpan position,
        bool force = false)
    {
        if (currentLyricsDocument is not { IsSynchronized: true } document)
        {
            if (!force && CurrentLyricLine is null) return;

            currentLyricLineIndex = -1;
            CurrentLyricLine = null;
            return;
        }

        var index = document.FindCurrentLineIndex(
            position,
            TimeSpan.FromMilliseconds(LyricsOffsetMilliseconds));
        if (!force && index == currentLyricLineIndex) return;

        currentLyricLineIndex = index;
        for (var i = 0; i < LyricLines.Count; i++)
        {
            LyricLines[i].IsCurrent = i == index;
            LyricLines[i].IsPast = index >= 0 && i < index;
        }

        CurrentLyricLine = index >= 0 && index < LyricLines.Count
            ? LyricLines[index]
            : null;
    }

    private void AdjustLyricsOffset(int deltaMilliseconds) =>
        SetLyricsOffset(LyricsOffsetMilliseconds + deltaMilliseconds);

    private void SetLyricsOffset(int offsetMilliseconds)
    {
        if (CurrentTrack is null || !CanAdjustLyricsOffset) return;

        var clamped = Math.Clamp(
            offsetMilliseconds,
            AppSettings.MinimumLyricsOffsetMilliseconds,
            AppSettings.MaximumLyricsOffsetMilliseconds);
        if (clamped == LyricsOffsetMilliseconds) return;

        LyricsOffsetMilliseconds = clamped;
        if (clamped == 0)
            settings.LyricsOffsetsByPath.Remove(CurrentTrack.FilePath);
        else
            settings.LyricsOffsetsByPath[CurrentTrack.FilePath] = clamped;

        SaveSettings(force: true);
        UpdateCurrentLyricLine(audioPlayer.CurrentTime, force: true);
    }

    private int GetSavedLyricsOffset(string filePath) =>
        settings.LyricsOffsetsByPath.TryGetValue(filePath, out var offset)
            ? Math.Clamp(
                offset,
                AppSettings.MinimumLyricsOffsetMilliseconds,
                AppSettings.MaximumLyricsOffsetMilliseconds)
            : 0;

    private void SetLyricsPresentationState(LyricsPresentationState state)
    {
        lyricsPresentationState = state;
        OnPropertyChanged(nameof(LyricsStatusLabel));
        OnPropertyChanged(nameof(LyricsSourceLabel));
    }

    private void RefreshLyricsLocalizedLabels()
    {
        OnPropertyChanged(nameof(LyricsStatusLabel));
        OnPropertyChanged(nameof(LyricsSourceLabel));
    }

    private void NotifyLyricsCommandsCanExecuteChanged()
    {
        DecreaseLyricsOffsetCommand.NotifyCanExecuteChanged();
        IncreaseLyricsOffsetCommand.NotifyCanExecuteChanged();
        ResetLyricsOffsetCommand.NotifyCanExecuteChanged();
        ReloadLyricsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanReloadLyrics));
    }

    private enum LyricsPresentationState
    {
        NoTrack,
        Loading,
        Ready,
        NotFound,
        Unavailable,
        LoadFailed
    }
}
