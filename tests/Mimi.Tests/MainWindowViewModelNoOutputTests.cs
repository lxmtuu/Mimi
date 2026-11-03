using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Mimi.Behaviors;
using Mimi.Core.Playback;
using Mimi.Localization;
using Mimi.Models;
using Mimi.Services;
using Mimi.Theming;
using Mimi.ViewModels;
using NAudio.Wave;

namespace Mimi.Tests;

[Collection("Item click behavior")]
public sealed class MainWindowViewModelNoOutputTests
{
    [Fact]
    public void PlayTrack_WhenOutputUnavailable_RetainsCurrentTrackQueueAndShowsNotice_ThenResumesAfterRecovery()
    {
        RunOnSta(() =>
        {
            var outputError = "Simulated Windows audio endpoint is unavailable.";
            var audio = new FakeAudioPlayer
            {
                OutputStatus = AudioOutputStatus.Unavailable(outputError),
                PlayFailure = new AudioOutputUnavailableException(
                    AudioOutputStatus.Unavailable(outputError))
            };
            var dialogs = new FakeDialogService();
            var userData = new FakeUserDataService();
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                userData,
                dialogs);

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                var first = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\cyan.wav",
                        MetadataTitle = "Cyan Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);
                var second = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\magenta.wav",
                        MetadataTitle = "Magenta Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);

                viewModel.Tracks.Add(first);
                viewModel.Tracks.Add(second);

                viewModel.PlayTrackCommand.Execute(first);

                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(1, audio.PlayCalls);
                Assert.Same(first, viewModel.CurrentTrack);
                Assert.Same(first, viewModel.SelectedTrack);
                Assert.False(viewModel.IsPlaying);
                Assert.False(first.HasPlaybackError);
                Assert.False(first.Track.HasError);
                Assert.False(first.Track.IsMissing);
                Assert.Equal("No audio output available", viewModel.AudioOutputLabel);
                Assert.True(viewModel.HasAudioOutputNotice);
                Assert.Equal(outputError, viewModel.AudioOutputNotice);
                Assert.Single(viewModel.UpcomingQueueItems);
                Assert.Same(second, viewModel.UpcomingQueueItems[0].Track);
                Assert.Contains(dialogs.Warnings, warning =>
                    warning.Title == "Audio output" &&
                    warning.Message.Contains("song is loaded", StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(userData.LastSavedSettings?.PlaybackSession);
                Assert.Equal(first.FilePath, userData.LastSavedSettings!.PlaybackSession!.CurrentTrackPath);

                audio.PlayFailure = null;
                audio.SetOutputStatus(new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready."));

                Dispatcher.CurrentDispatcher.Invoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() => { }));

                Assert.Contains("WASAPI shared", viewModel.AudioOutputLabel);
                Assert.Contains("Test Speakers", viewModel.AudioOutputLabel);
                Assert.Contains("Press Play to continue", viewModel.AudioOutputNotice);

                viewModel.PlayPauseCommand.Execute(null);

                Assert.True(viewModel.IsPlaying);
                Assert.Empty(viewModel.AudioOutputNotice);
                Assert.Same(first, viewModel.CurrentTrack);
                Assert.Single(viewModel.UpcomingQueueItems);
                Assert.Same(second, viewModel.UpcomingQueueItems[0].Track);
                Assert.Equal(2, audio.PlayCalls);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void QueueCommands_ReflectEmptySelectionAndBoundaryStates()
    {
        RunOnSta(() =>
        {
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                new FakeUserDataService(),
                new FakeDialogService());

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                Assert.False(viewModel.PlayQueueItemCommand.CanExecute(null));
                Assert.False(viewModel.RemoveQueueItemCommand.CanExecute(null));
                Assert.False(
                    viewModel.MoveSelectedQueueItemUpCommand.CanExecute(null));
                Assert.False(
                    viewModel.MoveSelectedQueueItemDownCommand.CanExecute(null));
                Assert.False(viewModel.ClearUpcomingQueueCommand.CanExecute(null));

                static TrackItemViewModel CreateTrack(string fileName, string title) =>
                    new(
                        new Track
                        {
                            FilePath = @$"C:\fixtures\{fileName}",
                            MetadataTitle = title,
                            Duration = TimeSpan.FromSeconds(30)
                        },
                        isFavorite: false);

                var first = CreateTrack("cyan.wav", "Cyan Signal");
                var second = CreateTrack("magenta.wav", "Magenta Signal");
                var third = CreateTrack("amber.wav", "Amber Signal");
                viewModel.Tracks.Add(first);
                viewModel.Tracks.Add(second);
                viewModel.Tracks.Add(third);

                viewModel.PlayTrackCommand.Execute(first);

                Assert.Equal(2, viewModel.UpcomingQueueItems.Count);
                Assert.NotNull(viewModel.SelectedQueueItem);
                var selected = viewModel.SelectedQueueItem!;
                Assert.Same(second, selected.Track);
                Assert.True(viewModel.PlayQueueItemCommand.CanExecute(selected));
                Assert.True(viewModel.RemoveQueueItemCommand.CanExecute(selected));
                Assert.False(
                    viewModel.MoveSelectedQueueItemUpCommand.CanExecute(null));
                Assert.True(
                    viewModel.MoveSelectedQueueItemDownCommand.CanExecute(null));
                Assert.True(viewModel.ClearUpcomingQueueCommand.CanExecute(null));

                viewModel.MoveSelectedQueueItemDownCommand.Execute(null);

                Assert.Same(selected, viewModel.SelectedQueueItem);
                Assert.Same(third, viewModel.UpcomingQueueItems[0].Track);
                Assert.Same(second, viewModel.UpcomingQueueItems[1].Track);
                Assert.True(
                    viewModel.MoveSelectedQueueItemUpCommand.CanExecute(null));
                Assert.False(
                    viewModel.MoveSelectedQueueItemDownCommand.CanExecute(null));

                viewModel.ClearUpcomingQueueCommand.Execute(null);

                Assert.Empty(viewModel.UpcomingQueueItems);
                Assert.Null(viewModel.SelectedQueueItem);
                Assert.False(
                    viewModel.PlayQueueItemCommand.CanExecute(
                        viewModel.SelectedQueueItem));
                Assert.False(viewModel.RemoveQueueItemCommand.CanExecute(selected));
                Assert.False(
                    viewModel.MoveSelectedQueueItemUpCommand.CanExecute(null));
                Assert.False(
                    viewModel.MoveSelectedQueueItemDownCommand.CanExecute(null));
                Assert.False(viewModel.ClearUpcomingQueueCommand.CanExecute(null));
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void PlaybackIndicatorAndLibraryEmptyStates_ReflectCurrentTrackAndVisibleRows()
    {
        RunOnSta(() =>
        {
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                new FakeUserDataService(),
                new FakeDialogService());

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                Assert.True(viewModel.IsLibraryReady);
                Assert.False(viewModel.HasAnyTracks);
                Assert.False(viewModel.HasVisibleTracks);

                static TrackItemViewModel CreateTrack(string fileName, string title) =>
                    new(
                        new Track
                        {
                            FilePath = @$"C:\fixtures\{fileName}",
                            MetadataTitle = title,
                            Duration = TimeSpan.FromSeconds(30)
                        },
                        isFavorite: false);

                var first = CreateTrack("cyan.wav", "Cyan Signal");
                var second = CreateTrack("magenta.wav", "Magenta Signal");
                viewModel.Tracks.Add(first);
                viewModel.Tracks.Add(second);

                // SearchText reconfigures the view synchronously, which also
                // refreshes the empty-state inputs used by the library overlay.
                viewModel.SearchText = "Signal";
                Assert.True(viewModel.HasAnyTracks);
                Assert.True(viewModel.HasVisibleTracks);

                viewModel.PlayTrackCommand.Execute(first);

                Assert.True(first.IsCurrentTrack);
                Assert.True(first.IsCurrentTrackPlaying);
                Assert.Equal("Now playing", first.CurrentPlaybackIndicatorLabel);
                Assert.False(second.IsCurrentTrack);
                Assert.False(second.IsCurrentTrackPlaying);
                Assert.Equal(string.Empty, second.CurrentPlaybackIndicatorLabel);

                viewModel.PausePlaybackCommand.Execute(null);

                Assert.True(first.IsCurrentTrack);
                Assert.False(first.IsCurrentTrackPlaying);
                Assert.Equal(
                    "Current song, paused",
                    first.CurrentPlaybackIndicatorLabel);

                viewModel.PlayTrackCommand.Execute(second);

                Assert.False(first.IsCurrentTrack);
                Assert.False(first.IsCurrentTrackPlaying);
                Assert.Equal(string.Empty, first.CurrentPlaybackIndicatorLabel);
                Assert.True(second.IsCurrentTrack);
                Assert.True(second.IsCurrentTrackPlaying);
                Assert.Equal("Now playing", second.CurrentPlaybackIndicatorLabel);

                viewModel.SearchText = "no-such-song";

                Assert.True(viewModel.HasAnyTracks);
                Assert.False(viewModel.HasVisibleTracks);

                viewModel.SearchText = string.Empty;

                Assert.True(viewModel.HasAnyTracks);
                Assert.True(viewModel.HasVisibleTracks);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void PlayTrack_WhenTrackIsAlreadyCurrent_DoesNotReloadAndResumesWhenPaused()
    {
        RunOnSta(() =>
        {
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                new FakeUserDataService(),
                new FakeDialogService());

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                var track = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\cyan.wav",
                        MetadataTitle = "Cyan Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);
                viewModel.Tracks.Add(track);

                viewModel.PlayTrackCommand.Execute(track);

                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(1, audio.PlayCalls);
                Assert.Same(track, viewModel.CurrentTrack);
                Assert.True(viewModel.IsPlaying);

                // Clicking the currently playing row must not restart it.
                viewModel.PlayTrackCommand.Execute(track);

                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(1, audio.PlayCalls);
                Assert.True(viewModel.IsPlaying);

                viewModel.PausePlaybackCommand.Execute(null);
                Assert.False(viewModel.IsPlaying);

                // Clicking the paused current row resumes without reloading.
                viewModel.PlayTrackCommand.Execute(track);

                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(2, audio.PlayCalls);
                Assert.True(viewModel.IsPlaying);
                Assert.Same(track, viewModel.CurrentTrack);
                Assert.Same(track, viewModel.SelectedTrack);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void SingleClickOnBoundTrackList_UpdatesNowPlayingWithoutPlayButton()
    {
        RunOnSta(() =>
        {
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                new FakeUserDataService(),
                new FakeDialogService());

            Window? window = null;
            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                var first = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\cyan.wav",
                        MetadataTitle = "Cyan Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);
                var second = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\magenta.wav",
                        MetadataTitle = "Magenta Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);
                viewModel.Tracks.Add(first);
                viewModel.Tracks.Add(second);

                var listView = new ListView
                {
                    DataContext = viewModel,
                    ItemsSource = viewModel.TracksView,
                    SelectionMode = SelectionMode.Extended
                };
                listView.SetBinding(
                    System.Windows.Controls.Primitives.Selector
                        .SelectedItemProperty,
                    new Binding(nameof(MainWindowViewModel.SelectedTrack))
                    {
                        Mode = BindingMode.TwoWay
                    });
                ItemClickCommandBehavior.SetCommand(
                    listView,
                    viewModel.PlayTrackCommand);
                window = new Window
                {
                    Width = 600,
                    Height = 400,
                    Content = listView
                };
                window.Show();
                window.UpdateLayout();

                var container = Assert.IsType<ListViewItem>(
                    listView.ItemContainerGenerator.ContainerFromIndex(1));
                Assert.Same(
                    second,
                    listView.ItemContainerGenerator.ItemFromContainer(
                        container));
                Assert.Same(
                    viewModel.PlayTrackCommand,
                    ItemClickCommandBehavior.GetCommand(listView));
                Assert.True(viewModel.PlayTrackCommand.CanExecute(second));

                // The completed click must still activate playback even if
                // an element in the row consumes the bubbling MouseUp.
                container.AddHandler(
                    Mouse.MouseUpEvent,
                    new MouseButtonEventHandler(
                        (_, args) => args.Handled = true));
                container.RaiseEvent(
                    new MouseButtonEventArgs(
                        Mouse.PrimaryDevice,
                        Environment.TickCount,
                        MouseButton.Left)
                    {
                        RoutedEvent = Mouse.PreviewMouseDownEvent
                    });
                container.RaiseEvent(
                    new MouseButtonEventArgs(
                        Mouse.PrimaryDevice,
                        Environment.TickCount,
                        MouseButton.Left)
                    {
                        RoutedEvent = Mouse.MouseUpEvent
                    });

                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(1, audio.PlayCalls);
                Assert.Same(second, viewModel.CurrentTrack);
                Assert.Same(second, viewModel.SelectedTrack);
                Assert.Equal(second.Title, viewModel.NowPlayingTitle);
                Assert.True(viewModel.IsPlaying);
            }
            finally
            {
                window?.Close();
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void PlaybackStartedBeforeStartupRestore_IsNotOverwrittenBySavedSession()
    {
        RunOnSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));

            const string savedPath = @"C:\fixtures\saved.wav";
            const string chosenPath = @"C:\fixtures\chosen.wav";
            var loadTracksGate =
                new TaskCompletionSource<IReadOnlyList<Track>>();
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var userData = new FakeUserDataService
            {
                InitialSettings = new AppSettings
                {
                    LibraryView = new LibraryViewSettings
                    {
                        SelectedTrackPath = savedPath
                    },
                    PlaybackSession = new PlaybackSessionSettings
                    {
                        CurrentTrackPath = savedPath,
                        PositionSeconds = 5,
                        QueueTrackPaths = [savedPath, chosenPath],
                        UpcomingTrackPaths = [savedPath, chosenPath]
                    }
                }
            };
            var catalog = new FakeLibraryCatalog
            {
                LoadTracksGate = loadTracksGate
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                catalog,
                userData,
                new FakeDialogService());

            try
            {
                Assert.False(viewModel.InitializationTask.IsCompleted);

                var chosen = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = chosenPath,
                        MetadataTitle = "Chosen first click",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);
                viewModel.Tracks.Add(chosen);

                // Simulate the user winning the race against asynchronous
                // startup restore and starting a track first.
                viewModel.PlayTrackCommand.Execute(chosen);
                Assert.Same(chosen, viewModel.CurrentTrack);
                Assert.True(viewModel.IsPlaying);

                loadTracksGate.SetResult(
                [
                    new Track
                    {
                        FilePath = chosenPath,
                        MetadataTitle = "Chosen first click",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    new Track
                    {
                        FilePath = savedPath,
                        MetadataTitle = "Previously saved session",
                        Duration = TimeSpan.FromSeconds(30)
                    }
                ]);
                PumpDispatcherUntilCompleted(viewModel.InitializationTask);

                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(1, audio.PlayCalls);
                Assert.Same(chosen, viewModel.CurrentTrack);
                Assert.Same(chosen, viewModel.SelectedTrack);
                Assert.True(viewModel.IsPlaying);
            }
            finally
            {
                if (!loadTracksGate.Task.IsCompleted)
                    loadTracksGate.SetResult([]);
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void Initialization_WhenTrackListViewIsBound_LoadsStoredTracks()
    {
        RunOnSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));

            var loadTracksGate =
                new TaskCompletionSource<IReadOnlyList<Track>>();
            var dialogs = new FakeDialogService();
            var viewModel = new MainWindowViewModel(
                new FakeAudioPlayer
                {
                    OutputStatus = new AudioOutputStatus(
                        isAvailable: true,
                        AudioOutputKind.WasapiShared,
                        "Test Speakers",
                        "Audio output is ready.")
                },
                new FakeLibraryService(),
                new FakeLibraryCatalog
                {
                    LoadTracksGate = loadTracksGate
                },
                new FakeUserDataService(),
                dialogs);

            Window? window = null;
            try
            {
                Assert.False(viewModel.InitializationTask.IsCompleted);

                // This host is the missing condition from the ViewModel-only
                // startup test: a real Selector consumes TracksView and its
                // currency state while AddTracksToCollection mutates Tracks.
                var listView = new ListView
                {
                    DataContext = viewModel,
                    ItemsSource = viewModel.TracksView,
                    SelectionMode = SelectionMode.Extended
                };
                listView.SetBinding(
                    System.Windows.Controls.Primitives.Selector
                        .SelectedItemProperty,
                    new Binding(nameof(MainWindowViewModel.SelectedTrack))
                    {
                        Mode = BindingMode.TwoWay
                    });
                window = new Window
                {
                    Width = 600,
                    Height = 400,
                    Content = listView
                };
                window.Show();
                window.UpdateLayout();

                loadTracksGate.SetResult(
                [
                    new Track
                    {
                        FilePath = @"C:\fixtures\cyan.wav",
                        MetadataTitle = "Cyan Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    new Track
                    {
                        FilePath = @"C:\fixtures\magenta.wav",
                        MetadataTitle = "Magenta Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    }
                ]);
                PumpDispatcherUntilCompleted(viewModel.InitializationTask);
                window.UpdateLayout();

                Assert.Equal(2, viewModel.Tracks.Count);
                Assert.Equal(2, listView.Items.Count);
                Assert.Empty(dialogs.Warnings);
            }
            finally
            {
                if (!loadTracksGate.Task.IsCompleted)
                    loadTracksGate.SetResult([]);
                window?.Close();
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void CommunityTheme_LoadsFromThemesFolderAppliesAndPersists()
    {
        RunOnSta(() =>
        {
            var userData = new FakeUserDataService
            {
                InitialSettings = new AppSettings
                {
                    UiTheme = "alpha-one"
                }
            };
            var themesDirectory = Path.Combine(userData.DataDirectory, "themes");
            Directory.CreateDirectory(themesDirectory);
            File.WriteAllText(
                Path.Combine(themesDirectory, "alpha-one.json"),
                CommunityThemeLoaderTests.BuildThemeJson(
                    "alpha-one",
                    "Alpha One"));
            var themeService = new ThemeService(userData);
            var viewModel = new MainWindowViewModel(
                new FakeAudioPlayer
                {
                    OutputStatus = new AudioOutputStatus(
                        true,
                        AudioOutputKind.WasapiShared,
                        "Test Speakers",
                        "Audio output is ready.")
                },
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                userData,
                new FakeDialogService(),
                themeService: themeService);

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                Assert.Equal("alpha-one", viewModel.UiTheme);
                Assert.Equal("alpha-one", themeService.CurrentThemeId);
                Assert.Contains(
                    viewModel.ThemeOptions,
                    option => option.Id == "alpha-one" &&
                              option.Name == "Alpha One" &&
                              !option.IsBuiltIn);
                Assert.Equal(
                    BuiltInThemes.All.Count + 1,
                    viewModel.ThemeOptions.Count);

                viewModel.UiTheme = BuiltInThemes.SakuraDreamId;
                Assert.Equal(
                    BuiltInThemes.SakuraDreamId,
                    userData.LastSavedSettings?.UiTheme);

                viewModel.UiTheme = "alpha-one";
                Assert.Equal(
                    "alpha-one",
                    userData.LastSavedSettings?.UiTheme);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void ThemePreference_AppliesLoadedThemeNormalizesAndPersistsChanges()
    {
        RunOnSta(() =>
        {
            var themeService = new ThemeService();
            var userData = new FakeUserDataService
            {
                InitialSettings = new AppSettings
                {
                    UiTheme = " Sakura_Dream "
                }
            };
            var viewModel = new MainWindowViewModel(
                new FakeAudioPlayer
                {
                    OutputStatus = new AudioOutputStatus(
                        true,
                        AudioOutputKind.WasapiShared,
                        "Test Speakers",
                        "Audio output is ready.")
                },
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                userData,
                new FakeDialogService(),
                themeService: themeService);

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                Assert.Equal(BuiltInThemes.SakuraDreamId, viewModel.UiTheme);
                Assert.Equal(
                    BuiltInThemes.SakuraDreamId,
                    themeService.CurrentThemeId);

                viewModel.UiTheme = "theme-that-does-not-exist";

                Assert.Equal(BuiltInThemes.DefaultId, viewModel.UiTheme);
                Assert.Equal(BuiltInThemes.DefaultId, themeService.CurrentThemeId);
                Assert.Equal(
                    BuiltInThemes.DefaultId,
                    userData.LastSavedSettings?.UiTheme);

                viewModel.UiTheme = BuiltInThemes.MonoFocusId;

                Assert.Equal(BuiltInThemes.MonoFocusId, themeService.CurrentThemeId);
                Assert.Equal(
                    BuiltInThemes.MonoFocusId,
                    userData.LastSavedSettings?.UiTheme);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void LanguageSwitch_LocalizesFilterOptionsAndPreservesKnownSelections()
    {
        RunOnSta(() =>
        {
            var localization = new LocalizationService();
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                new FakeUserDataService(),
                new FakeDialogService(),
                localizationService: localization);

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();
                viewModel.UiLanguage = UiLanguages.English;
                var track = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\unknown.mp3",
                        MetadataTitle = "Unknown metadata",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false,
                    localization);
                viewModel.Tracks.Add(track);

                viewModel.UiLanguage = UiLanguages.Vietnamese;

                Assert.Contains("Tất cả nghệ sĩ", viewModel.ArtistFilterOptions);
                Assert.Contains("Nghệ sĩ không rõ", viewModel.ArtistFilterOptions);
                Assert.Equal(
                    ["Tất cả trạng thái", "Sẵn sàng", "Có lỗi", "Thiếu file"],
                    viewModel.PlaybackStateFilterOptions);

                viewModel.SelectedArtistFilter = "Nghệ sĩ không rõ";
                viewModel.SelectedPlaybackStateFilter = "Có lỗi";
                Assert.True(viewModel.HasActiveAdvancedFilters);

                viewModel.UiLanguage = UiLanguages.English;

                Assert.Contains("All artists", viewModel.ArtistFilterOptions);
                Assert.Contains("Unknown artist", viewModel.ArtistFilterOptions);
                Assert.Equal(
                    ["All states", "Ready", "Has error", "Missing"],
                    viewModel.PlaybackStateFilterOptions);
                Assert.Equal("Unknown artist", viewModel.SelectedArtistFilter);
                Assert.Equal("Has error", viewModel.SelectedPlaybackStateFilter);
                Assert.True(viewModel.HasActiveAdvancedFilters);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void RealAudioService_DeviceInvalidation_PreservesQueueSessionAndResumesThroughViewModel()
    {
        RunOnSta(() =>
        {
            WithSilentWaveFile(path =>
            {
                var firstOutput = new RecoveryWavePlayer();
                var recoveredOutput = new RecoveryWavePlayer();
                var factoryCalls = 0;
                using var audio = new AudioService(
                    AudioOutputBackendPreference.ForceWasapiShared,
                    _ =>
                    {
                        factoryCalls++;
                        return factoryCalls switch
                        {
                            1 => firstOutput,
                            2 => throw new IOException("Simulated endpoint disconnect."),
                            3 => recoveredOutput,
                            _ => throw new InvalidOperationException("Unexpected output retry.")
                        };
                    },
                    () => throw new InvalidOperationException("WaveOut fallback must stay disabled."),
                    useDeviceWatcher: false);

                var userData = new FakeUserDataService();
                var dialogs = new FakeDialogService();
                var viewModel = new MainWindowViewModel(
                    audio, new FakeLibraryService(), new FakeLibraryCatalog(), userData, dialogs);

                try
                {
                    viewModel.InitializationTask.GetAwaiter().GetResult();
                    var cyan = new TrackItemViewModel(new Track
                    {
                        FilePath = path, MetadataTitle = "Cyan Signal",
                        Duration = TimeSpan.FromSeconds(10)
                    }, isFavorite: false);
                    var magenta = new TrackItemViewModel(new Track
                    {
                        FilePath = path + ".magenta", MetadataTitle = "Magenta Signal",
                        Duration = TimeSpan.FromSeconds(10)
                    }, isFavorite: false);
                    viewModel.Tracks.Add(cyan);
                    viewModel.Tracks.Add(magenta);

                    viewModel.PlayTrackCommand.Execute(cyan);
                    Assert.Same(cyan, viewModel.CurrentTrack);
                    Assert.True(viewModel.IsPlaying);
                    firstOutput.AdvanceBy(TimeSpan.FromSeconds(1));
                    var retainedPosition = audio.CurrentTime;
                    Assert.InRange(Math.Abs(retainedPosition.TotalSeconds - 1), 0, 0.06);

                    firstOutput.RaiseStopped(new COMException(
                        "Simulated endpoint invalidation.", unchecked((int)0x88890004)));

                    Assert.True(SpinWait.SpinUntil(
                        () => !audio.OutputStatus.IsAvailable && !audio.IsPlaying,
                        TimeSpan.FromSeconds(5)),
                        "AudioService did not report the simulated device loss.");

                    Dispatcher.CurrentDispatcher.Invoke(
                        DispatcherPriority.ApplicationIdle, new Action(() => { }));

                    Assert.Same(cyan, viewModel.CurrentTrack);
                    Assert.False(viewModel.IsPlaying);
                    Assert.False(cyan.HasPlaybackError);
                    Assert.False(cyan.Track.HasError);
                    Assert.False(cyan.Track.IsMissing);
                    Assert.Single(viewModel.UpcomingQueueItems);
                    Assert.Same(magenta, viewModel.UpcomingQueueItems[0].Track);
                    Assert.Equal("No audio output available", viewModel.AudioOutputLabel);
                    Assert.Contains("playback is paused", viewModel.AudioOutputNotice,
                        StringComparison.OrdinalIgnoreCase);

                    var saved = userData.LastSavedSettings?.PlaybackSession;
                    Assert.NotNull(saved);
                    Assert.Equal(path, saved!.CurrentTrackPath);
                    Assert.InRange(Math.Abs(saved.PositionSeconds - retainedPosition.TotalSeconds), 0, 0.06);
                    Assert.Contains(path + ".magenta", saved.UpcomingTrackPaths);

                    viewModel.PlayPauseCommand.Execute(null);

                    Assert.Equal(3, factoryCalls);
                    Assert.True(audio.OutputStatus.IsAvailable);
                    Assert.True(viewModel.IsPlaying);
                    Assert.Same(cyan, viewModel.CurrentTrack);
                    Assert.Empty(viewModel.AudioOutputNotice);
                    Assert.Single(viewModel.UpcomingQueueItems);
                    Assert.Same(magenta, viewModel.UpcomingQueueItems[0].Track);
                    Assert.Equal(PlaybackState.Playing, recoveredOutput.PlaybackState);
                    Assert.InRange(Math.Abs((audio.CurrentTime - retainedPosition).TotalMilliseconds), 0, 60);
                }
                finally
                {
                    viewModel.Dispose();
                }
            }, 10000);
        });
    }

    private static void WithSilentWaveFile(Action<string> action, int durationMilliseconds)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mimi-vm-recovery-{Guid.NewGuid():N}.wav");
        try
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            var sampleCount = sampleRate * durationMilliseconds / 1000;
            var dataLength = sampleCount * channels * bitsPerSample / 8;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            writer.Write(new byte[dataLength]);
            writer.Dispose();
            stream.Dispose();
            action(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void RadioPlayback_UsesSeparateLiveSource_RegistersOneClick_AndLocalTrackRetakesPlayback()
    {
        RunOnSta(() =>
        {
            var stationUuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var station = new RadioStation(
                stationUuid,
                "Mimi Wave",
                new Uri("https://stream.example/live.mp3"),
                new Uri("https://station.example/"),
                null,
                "Japan",
                "Japanese",
                ["anime"],
                "MP3",
                128,
                10,
                20);
            var radio = new FakeRadioStationService
            {
                TopStations = [station]
            };
            var audio = new FakeAudioPlayer
            {
                OutputStatus = new AudioOutputStatus(
                    isAvailable: true,
                    AudioOutputKind.WasapiShared,
                    "Test Speakers",
                    "Audio output is ready.")
            };
            var viewModel = new MainWindowViewModel(
                audio,
                new FakeLibraryService(),
                new FakeLibraryCatalog(),
                new FakeUserDataService(),
                new FakeDialogService(),
                radioStationService: radio);

            try
            {
                viewModel.InitializationTask.GetAwaiter().GetResult();

                PumpDispatcherUntilCompleted(
                    viewModel.ShowRadioCommand.ExecuteAsync(null));

                Assert.True(viewModel.IsRadioOpen);
                Assert.True(viewModel.IsRadioContentVisible);
                Assert.False(viewModel.IsLibraryContentVisible);
                Assert.Equal(1, radio.TopCalls);
                var stationViewModel = Assert.Single(viewModel.RadioStations);

                PumpDispatcherUntilCompleted(
                    viewModel.PlayRadioStationCommand.ExecuteAsync(stationViewModel));

                Assert.Same(stationViewModel, viewModel.CurrentRadioStation);
                Assert.Null(viewModel.CurrentTrack);
                Assert.True(viewModel.HasCurrentPlaybackSource);
                Assert.True(viewModel.IsLivePlayback);
                Assert.True(viewModel.IsPlaying);
                Assert.Equal(1, audio.StreamLoadCalls);
                Assert.Equal(1, audio.PlayCalls);
                Assert.Equal(station.StreamUrl, audio.LoadedStreamUrl);
                Assert.Equal([stationUuid], radio.Clicks);
                Assert.Equal("Mimi Wave", viewModel.NowPlayingTitle);
                Assert.Contains("Internet Radio", viewModel.NowPlayingSubtitle);
                Assert.Equal("LIVE", viewModel.DurationLabel);
                Assert.True(stationViewModel.IsCurrent);
                Assert.True(stationViewModel.IsPlaying);

                PumpDispatcherUntilCompleted(
                    viewModel.PlayRadioStationCommand.ExecuteAsync(stationViewModel));

                Assert.False(viewModel.IsPlaying);
                Assert.Equal(1, audio.StreamLoadCalls);
                Assert.Single(radio.Clicks);

                viewModel.PlayPauseCommand.Execute(null);
                PumpDispatcherUntil(() =>
                    audio.StreamLoadCalls == 2 &&
                    viewModel.IsPlaying &&
                    !viewModel.IsRadioConnecting);

                Assert.Equal(2, audio.PlayCalls);
                Assert.Single(radio.Clicks);

                var localTrack = new TrackItemViewModel(
                    new Track
                    {
                        FilePath = @"C:\fixtures\local.wav",
                        MetadataTitle = "Local Signal",
                        Duration = TimeSpan.FromSeconds(30)
                    },
                    isFavorite: false);
                viewModel.Tracks.Add(localTrack);

                viewModel.PlayTrackCommand.Execute(localTrack);

                Assert.Same(localTrack, viewModel.CurrentTrack);
                Assert.Null(viewModel.CurrentRadioStation);
                Assert.False(viewModel.IsLivePlayback);
                Assert.Equal(1, audio.LoadCalls);
                Assert.Equal(3, audio.PlayCalls);
                Assert.Single(radio.Clicks);
                Assert.False(stationViewModel.IsCurrent);
                Assert.False(stationViewModel.IsPlaying);
            }
            finally
            {
                viewModel.Dispose();
            }
        });
    }

    private static void RunOnSta(Action action)
    {
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
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new Xunit.Sdk.XunitException(
                $"STA/WPF test failed: {failure}");
    }

    private static void PumpDispatcherUntilCompleted(Task task)
    {
        while (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        task.GetAwaiter().GetResult();
    }

    private static void PumpDispatcherUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Timed out waiting for the dispatcher condition.");

            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private sealed class RecoveryWavePlayer : IWavePlayer
    {
        private IWaveProvider? provider;
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public float Volume { get; set; } = 1f;
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public WaveFormat OutputWaveFormat =>
            provider?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        public void Init(IWaveProvider waveProvider) =>
            provider = waveProvider ?? throw new ArgumentNullException(nameof(waveProvider));
        public void Play() => PlaybackState = PlaybackState.Playing;
        public void Pause() => PlaybackState = PlaybackState.Paused;
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public void AdvanceBy(TimeSpan duration)
        {
            if (provider is null) throw new InvalidOperationException("Output is not initialized.");
            var remaining = (long)Math.Round(
                provider.WaveFormat.AverageBytesPerSecond * duration.TotalSeconds);
            var buffer = new byte[Math.Min(8192, provider.WaveFormat.AverageBytesPerSecond)];
            while (remaining > 0)
            {
                var read = provider.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read <= 0) break;
                remaining -= read;
            }
        }
        public void RaiseStopped(Exception exception)
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs(exception));
        }
        public void Dispose()
        {
            PlaybackState = PlaybackState.Stopped;
            provider = null;
            PlaybackStopped = null;
        }
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public event EventHandler? PlaybackChanged;
        public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded
        {
            add { }
            remove { }
        }
        public event EventHandler<AudioOutputStatusChangedEventArgs>? OutputStatusChanged;

        public long PlaybackGeneration { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsLiveStream { get; private set; }
        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan Duration { get; private set; }
        public float Volume { get; set; }
        public AudioOutputStatus OutputStatus { get; set; }
        public AudioOutputUnavailableException? PlayFailure { get; set; }
        public int LoadCalls { get; private set; }
        public int StreamLoadCalls { get; private set; }
        public int PlayCalls { get; private set; }
        public Uri? LoadedStreamUrl { get; private set; }
        public Exception? StreamLoadFailure { get; set; }

        public void Load(string path)
        {
            LoadCalls++;
            PlaybackGeneration++;
            CurrentTime = TimeSpan.Zero;
            Duration = TimeSpan.FromSeconds(30);
            IsPlaying = false;
            IsLiveStream = false;
            LoadedStreamUrl = null;
        }

        public void LoadStream(Uri streamUrl)
        {
            StreamLoadCalls++;
            if (StreamLoadFailure is not null)
                throw StreamLoadFailure;

            PlaybackGeneration++;
            LoadedStreamUrl = streamUrl;
            CurrentTime = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            IsPlaying = false;
            IsLiveStream = true;
        }

        public void Play()
        {
            PlayCalls++;
            if (PlayFailure is not null)
                throw PlayFailure;

            IsPlaying = true;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            IsPlaying = false;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Toggle()
        {
            if (IsPlaying)
                Pause();
            else
                Play();
        }

        public void Stop()
        {
            IsPlaying = false;
            CurrentTime = TimeSpan.Zero;
            PlaybackGeneration++;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Seek(TimeSpan position) => CurrentTime = position;

        public Mimi.Services.VisualizerFrame GetVisualizerFrame() => null!;

        public float[] GetVisualizerLevels() => [];

        public string GetVisualizerDiagnostics() => "test fake";

        public void SetOutputStatus(AudioOutputStatus status)
        {
            OutputStatus = status;
            OutputStatusChanged?.Invoke(
                this,
                new AudioOutputStatusChangedEventArgs(status, playbackInterrupted: false));
        }

        public void Dispose()
        {
            PlaybackChanged = null;
            OutputStatusChanged = null;
        }
    }

    private sealed class FakeRadioStationService : IRadioStationService
    {
        public IReadOnlyList<RadioStation> TopStations { get; init; } = [];
        public List<string> Searches { get; } = [];
        public List<Guid> Clicks { get; } = [];
        public int TopCalls { get; private set; }

        public Task<IReadOnlyList<RadioStation>> GetTopStationsAsync(
            CancellationToken cancellationToken = default)
        {
            TopCalls++;
            return Task.FromResult(TopStations);
        }

        public Task<IReadOnlyList<RadioStation>> SearchStationsAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(query);
            return Task.FromResult<IReadOnlyList<RadioStation>>(
                TopStations
                    .Where(station => station.Name.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray());
        }

        public Task RegisterStationClickAsync(
            Guid stationUuid,
            CancellationToken cancellationToken = default)
        {
            Clicks.Add(stationUuid);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibraryService : ILibraryService
    {
        public IEnumerable<Track> ScanFiles(IEnumerable<string> paths) => [];
        public FolderScanResult ScanFolder(string folder, bool recursive = true) => new([], 0);
        public Task<IReadOnlyList<Track>> ScanFilesAsync(
            IEnumerable<string> paths,
            IProgress<LibraryScanProgress>? progress = null,
            IReadOnlyDictionary<string, Track>? knownTracks = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Track>>([]);
        public Task<FolderScanResult> ScanFolderAsync(
            string folder,
            bool recursive = true,
            IProgress<LibraryScanProgress>? progress = null,
            IReadOnlyDictionary<string, Track>? knownTracks = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FolderScanResult([], 0));
    }

    private sealed class FakeLibraryCatalog : ILibraryCatalog
    {
        public IReadOnlyList<Track> StoredTracks { get; init; } = [];

        public TaskCompletionSource<IReadOnlyList<Track>>? LoadTracksGate
        {
            get;
            init;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Track>> LoadTracksAsync(CancellationToken cancellationToken = default) =>
            LoadTracksGate?.Task ?? Task.FromResult(StoredTracks);
        public Task<IReadOnlyList<Track>> SearchTracksAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Track>>([]);
        public Task UpsertTracksAsync(IEnumerable<Track> tracks, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RemoveTrackAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SetFavoriteAsync(string filePath, bool isFavorite, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SetPlaybackErrorAsync(string filePath, bool hasError, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<long> RecordPlaybackStartedAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<bool> MarkPlaybackCompletedAsync(long playHistoryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Track>> LoadRecentTracksAsync(int maxCount = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Track>>([]);
        public Task<IReadOnlyList<Playlist>> LoadPlaylistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Playlist>>([]);
        public Task<Playlist> CreatePlaylistAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Playlist { Name = name });
        public Task<bool> RenamePlaylistAsync(long playlistId, string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DeletePlaylistAsync(long playlistId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Track>> LoadPlaylistTracksAsync(long playlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Track>>([]);
        public Task<bool> AddTrackToPlaylistAsync(long playlistId, string filePath, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RemoveTrackFromPlaylistAsync(long playlistId, string filePath, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> MovePlaylistTrackAsync(long playlistId, int fromPosition, int toPosition, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeUserDataService : IUserDataService
    {
        public string DataDirectory { get; } = Path.Combine(
            Path.GetTempPath(),
            $"mimi-viewmodel-test-{Guid.NewGuid():N}");

        public AppSettings? LastSavedSettings { get; private set; }
        public AppSettings InitialSettings { get; init; } = new();
        public void MigrateLegacyDataIfNeeded() { }
        public AppSettings LoadSettings() => InitialSettings;
        public void SaveSettings(AppSettings settings) => LastSavedSettings = settings;
        public IReadOnlyList<string> LoadLibraryPaths() => [];
        public void SaveLibraryPaths(IEnumerable<string> paths) { }
        public HashSet<string> LoadFavorites() => new(StringComparer.OrdinalIgnoreCase);
        public void SaveFavorites(IEnumerable<string> paths) { }
        public void ArchiveImportedLibraryData() { }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public List<(string Message, string Title)> Warnings { get; } = [];
        public IReadOnlyList<string> PickAudioFiles() => [];
        public string? PickMusicFolder() => null;
        public string? PromptText(string title, string prompt, string initialValue = "") => null;
        public bool Confirm(string message, string title) => false;
        public void ShowInformation(string message, string title = "Mimi") { }
        public void ShowWarning(string message, string title) => Warnings.Add((message, title));
    }
}
