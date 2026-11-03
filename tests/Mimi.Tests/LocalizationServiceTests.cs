using System.ComponentModel;
using Mimi.Localization;
using Mimi.Models;
using Mimi.ViewModels;

namespace Mimi.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void NeutralAndVietnameseResourcesExposeTheSameKeys()
    {
        var service = new LocalizationService();

        var neutralKeys = service.GetKeys(null);
        var vietnameseKeys = service.GetKeys("vi");

        Assert.NotEmpty(neutralKeys);
        Assert.Equal(neutralKeys, vietnameseKeys);
        Assert.Equal("Home", service.GetString("Nav_Home", "en-US"));
        Assert.Equal("Trang chủ", service.GetString("Nav_Home", "vi-VN"));
    }

    [Fact]
    public void GetStringReturnsCultureSpecificText()
    {
        var service = new LocalizationService();

        Assert.Equal("Home", service.GetString("Nav_Home", "en-US"));
        Assert.Equal("Trang chủ", service.GetString("Nav_Home", "vi-VN"));
        Assert.Equal(
            "Restore the previous session",
            service.GetString("Settings_RestorePreviousSession", "en-US"));
        Assert.Equal(
            "Khôi phục phiên trước",
            service.GetString("Settings_RestorePreviousSession", "vi-VN"));
        Assert.Equal(
            "Clear upcoming",
            service.GetString("Queue_ClearUpcoming", "en-US"));
        Assert.Equal(
            "Xoá hàng đợi sắp phát",
            service.GetString("Queue_ClearUpcoming", "vi-VN"));
        Assert.Equal(
            "All artists",
            service.GetString("Filter_AllArtists", "en-US"));
        Assert.Equal(
            "Tất cả nghệ sĩ",
            service.GetString("Filter_AllArtists", "vi-VN"));
        Assert.Equal(
            "Unknown genre",
            service.GetString("Metadata_UnknownGenre", "en-US"));
        Assert.Equal(
            "Thể loại không rõ",
            service.GetString("Metadata_UnknownGenre", "vi-VN"));
        Assert.Equal(
            "Playback error",
            service.GetString("Playback_ErrorTitle", "en-US"));
        Assert.Equal(
            "Lỗi phát nhạc",
            service.GetString("Playback_ErrorTitle", "vi-VN"));
        Assert.Equal(
            "Interface theme",
            service.GetString("Settings_Theme", "en-US"));
        Assert.Equal(
            "Chủ đề giao diện",
            service.GetString("Settings_Theme", "vi-VN"));
        Assert.Equal(
            "Sakura Dream",
            service.GetString("Theme_SakuraDream", "en-US"));
        Assert.Equal(
            "Giấc mơ Sakura",
            service.GetString("Theme_SakuraDream", "vi-VN"));
        Assert.Equal(
            "No audio output is available. Connect or enable an output device, then press Play.",
            service.GetString("Audio_MessageNoOutputPressPlay", "en-US"));
        Assert.Equal(
            "Không có đầu ra âm thanh. Hãy kết nối hoặc bật thiết bị đầu ra, rồi nhấn Phát.",
            service.GetString("Audio_MessageNoOutputPressPlay", "vi-VN"));
        Assert.Equal(
            "LYRICS",
            service.GetString("Lyrics_Title", "en-US"));
        Assert.Equal(
            "LỜI BÀI HÁT",
            service.GetString("Lyrics_Title", "vi-VN"));
        Assert.Equal(
            "No lyrics found. Add a same-name .lrc file beside the song.",
            service.GetString("Lyrics_NotFound", "en-US"));
        Assert.Equal(
            "Không tìm thấy lời. Hãy đặt file .lrc cùng tên cạnh bài hát.",
            service.GetString("Lyrics_NotFound", "vi-VN"));
    }

    [Fact]
    public void SetLanguageUpdatesIndexerAndRaisesIndexerNotification()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        var service = new LocalizationService();
        var changedProperties = new List<string?>();
        service.PropertyChanged += OnPropertyChanged;

        try
        {
            service.SetLanguage(UiLanguages.English);
            changedProperties.Clear();

            service.SetLanguage(UiLanguages.Vietnamese);

            Assert.Equal("vi-VN", service.CurrentCultureName);
            Assert.Equal("Trang chủ", service["Nav_Home"]);
            Assert.Contains("Item[]", changedProperties);
            Assert.Contains(string.Empty, changedProperties);

            service.SetLanguage(UiLanguages.English);
            Assert.Equal("Home", service["Nav_Home"]);
        }
        finally
        {
            service.PropertyChanged -= OnPropertyChanged;
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
            changedProperties.Add(e.PropertyName);
    }

    [Fact]
    public void ChildViewModelLabelsUseTheInjectedLocalizationService()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        var service = new LocalizationService();

        try
        {
            service.SetLanguage(UiLanguages.Vietnamese);
            var playlist = new PlaylistItemViewModel(
                new Playlist { Name = "Mix", TrackCount = 3 },
                service);
            var track = new TrackItemViewModel(
                new Track { FilePath = @"C:\Music\song.mp3" },
                isFavorite: false,
                service);

            Assert.Equal("3 bài", playlist.CountLabel);
            Assert.Equal("Thêm vào yêu thích", track.FavoriteToolTip);
            Assert.Equal("Nghệ sĩ không rõ", track.Artist);
            Assert.Equal("Album không rõ", track.Album);
            Assert.Equal("Thể loại không rõ", track.Genre);
            Assert.Equal("MP3  •  Nhạc cục bộ", track.Subtitle);

            track.IsFavorite = true;
            Assert.Equal("Xoá khỏi yêu thích", track.FavoriteToolTip);

            service.SetLanguage(UiLanguages.English);
            Assert.Equal("3 song(s)", playlist.CountLabel);
            Assert.Equal("Remove from favorites", track.FavoriteToolTip);
            Assert.Equal("Unknown artist", track.Artist);
            Assert.Equal("Unknown album", track.Album);
            Assert.Equal("Unknown genre", track.Genre);
            Assert.Equal("MP3  •  Local music", track.Subtitle);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void FormatUsesTheCurrentCultureResource()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        var service = new LocalizationService();

        try
        {
            service.SetLanguage(UiLanguages.Vietnamese);

            Assert.Equal(
                "3 bài trong thư viện của bạn",
                service.Format("TrackCount_Library", 3));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }
}
