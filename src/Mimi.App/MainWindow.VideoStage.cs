using System.Windows;
using System.Windows.Controls;

namespace Mimi;

/// <summary>
/// The video stage: while a YouTube video is playing, the same window turns
/// into a full-screen player instead of opening the visualizer Stage.
///
/// The official player is a WebView2 inside this window, and a WebView2 cannot
/// be handed to another window, so a second Stage window would mean a second
/// player: a reload, a second copy of the same sound and two timelines to keep
/// in step. Collapsing the shell around the existing player gives the same
/// full-screen picture with one player, one sound source and no interruption.
/// </summary>
public partial class MainWindow
{
    private bool isVideoStageOpen;
    private WindowState videoStageRestoreState = WindowState.Normal;
    private Thickness videoStageRestoreMargin;
    private CornerRadius videoStageRestoreRadius;
    private double navColumnRestoreMinWidth;
    private double mainColumnRestoreMinWidth;

    private void ToggleVideoStage()
    {
        if (isVideoStageOpen)
            ExitVideoStage();
        else
            EnterVideoStage();
    }

    private void EnterVideoStage()
    {
        if (isVideoStageOpen || !viewModel.IsYouTubePlaying) return;

        isVideoStageOpen = true;
        videoStageRestoreState = WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;
        videoStageRestoreMargin = ShellGrid.Margin;
        videoStageRestoreRadius = NowPlayingPanel.CornerRadius;
        navColumnRestoreMinWidth = NavColumn.MinWidth;
        mainColumnRestoreMinWidth = MainColumn.MinWidth;

        // Everything except Now Playing leaves the frame; the two holes it
        // leaves collapse to zero so the player card owns the window.
        AppHeader.Visibility = Visibility.Collapsed;
        AppTransport.Visibility = Visibility.Collapsed;
        NavRail.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Collapsed;
        AppHeaderRow.Height = new GridLength(0);
        AppTransportRow.Height = new GridLength(0);
        NavColumn.MinWidth = 0;
        MainColumn.MinWidth = 0;
        NavColumn.Width = new GridLength(0);
        MainColumn.Width = new GridLength(0);
        NowPlayingColumn.Width = new GridLength(1, GridUnitType.Star);
        ShellGrid.Margin = new Thickness(6);
        NowPlayingPanel.CornerRadius = new CornerRadius(10);

        WindowState = WindowState.Maximized;
        SizeChanged += MainWindow_VideoStageResized;
        FitVideoStageHeight();
    }

    private void ExitVideoStage()
    {
        if (!isVideoStageOpen) return;

        isVideoStageOpen = false;
        SizeChanged -= MainWindow_VideoStageResized;
        PlayerCard.ClearValue(FrameworkElement.HeightProperty);

        AppHeader.Visibility = Visibility.Visible;
        AppTransport.Visibility = Visibility.Visible;
        NavRail.Visibility = Visibility.Visible;
        MainContent.Visibility = Visibility.Visible;
        AppHeaderRow.Height = GridLength.Auto;
        AppTransportRow.Height = GridLength.Auto;
        NavColumn.MinWidth = navColumnRestoreMinWidth;
        MainColumn.MinWidth = mainColumnRestoreMinWidth;
        NavColumn.Width = new GridLength(210);
        MainColumn.Width = new GridLength(1, GridUnitType.Star);
        NowPlayingColumn.Width = new GridLength(270);
        ShellGrid.Margin = videoStageRestoreMargin;
        NowPlayingPanel.CornerRadius = videoStageRestoreRadius;
        WindowState = videoStageRestoreState;
    }

    private void MainWindow_VideoStageResized(object sender, SizeChangedEventArgs e) =>
        FitVideoStageHeight();

    /// <summary>
    /// The player card lives in a scrolling stack with a fixed height, so the
    /// stage has to size it: most of the window for the video, the rest for the
    /// title and transport under it.
    /// </summary>
    private void FitVideoStageHeight()
    {
        if (!isVideoStageOpen) return;

        var available = ActualHeight - 120;
        PlayerCard.Height = Math.Max(300, available * 0.72);
    }
}
