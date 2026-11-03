using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mimi.Services;

/// <summary>
/// Applies the Windows immersive dark title bar so Mimi's dark themes are
/// not framed by the stock white caption. On Windows 11 the caption and
/// caption-text colors are also taken from the active theme tokens, and
/// callers re-apply this when the theme changes. Best effort only: any
/// platform that lacks the DWM entry points simply keeps its caption.
/// </summary>
public static class WindowChromeHelper
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE (20 on Windows 10 2004+, 19 before).
    private const int UseImmersiveDarkModeBefore2004 = 19;
    private const int UseImmersiveDarkMode = 20;

    // DWMWA_CAPTION_COLOR / DWMWA_TEXT_COLOR (Windows 11+).
    private const int CaptionColor = 35;
    private const int CaptionTextColor = 36;

    public static void ApplyDarkTitleBar(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            EventHandler handler = null!;
            handler = (_, _) =>
            {
                window.SourceInitialized -= handler;
                ApplyDarkTitleBar(window);
            };
            window.SourceInitialized += handler;
            return;
        }

        try
        {
            var enabled = 1;
            var attribute = Environment.OSVersion.Version.Build >= 19041
                ? UseImmersiveDarkMode
                : UseImmersiveDarkModeBefore2004;
            _ = DwmSetWindowAttributeInt(
                handle,
                attribute,
                ref enabled,
                sizeof(int));

            if (Environment.OSVersion.Version.Build < 22000)
                return;

            if (Application.Current?.Resources["AppBackgroundColor"] is Color background)
            {
                var caption = ToColorRef(background);
                _ = DwmSetWindowAttributeUInt(
                    handle,
                    CaptionColor,
                    ref caption,
                    sizeof(uint));
            }

            if (Application.Current?.Resources["TextPrimaryColor"] is Color foreground)
            {
                var text = ToColorRef(foreground);
                _ = DwmSetWindowAttributeUInt(
                    handle,
                    CaptionTextColor,
                    ref text,
                    sizeof(uint));
            }
        }
        catch (Exception ex) when (
            ex is DllNotFoundException or
                EntryPointNotFoundException or
                BadImageFormatException)
        {
            // Caption styling is cosmetic; the window remains fully usable
            // with the system caption when DWM interop is unavailable.
            System.Diagnostics.Debug.WriteLine(
                $"Mimi could not style the window caption: {ex.Message}");
        }
    }

    private static uint ToColorRef(Color color) =>
        (uint)(color.R | (color.G << 8) | (color.B << 16));

    [DllImport(
        "dwmapi.dll",
        EntryPoint = "DwmSetWindowAttribute",
        PreserveSig = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttributeInt(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);

    [DllImport(
        "dwmapi.dll",
        EntryPoint = "DwmSetWindowAttribute",
        PreserveSig = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttributeUInt(
        IntPtr hwnd,
        int attribute,
        ref uint value,
        int size);
}
