namespace Mimi.Tests;

/// <summary>
/// Guards for the application icon assets (docs/design/BRANDING.md): the
/// multi-size ICO wired through the project file must stay parseable and
/// keep the standard Windows sizes, and the logo PNGs must stay square.
/// </summary>
public sealed class AppIconTests
{
    [Fact]
    public void ApplicationIconContainsStandardWindowsSizes()
    {
        var iconPath = Path.Combine(
            FindRepositoryRoot(), "src", "Mimi.App", "Assets", "mimi.ico");
        Assert.True(
            File.Exists(iconPath),
            $"Missing application icon at {iconPath}.");

        var bytes = File.ReadAllBytes(iconPath);
        Assert.True(bytes.Length > 6, "Application icon looks truncated.");
        // ICONDIR: reserved must be 0 and type 1 marks an icon (2 = cursor).
        Assert.Equal(0, (int)BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, (int)BitConverter.ToUInt16(bytes, 2));
        var count = BitConverter.ToUInt16(bytes, 4);
        Assert.True(count >= 8, $"Expected at least 8 icon sizes, got {count}.");
        Assert.True(
            bytes.Length >= 6 + (count * 16),
            "ICONDIR entries run past the end of the file.");

        var widths = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            // ICONDIRENTRY: width/height bytes use 0 to mean 256.
            var entryOffset = 6 + (i * 16);
            var width = bytes[entryOffset] == 0 ? 256 : bytes[entryOffset];
            var height = bytes[entryOffset + 1] == 0 ? 256 : bytes[entryOffset + 1];
            Assert.Equal(width, height);
            widths.Add(width);
        }

        Assert.Contains(16, widths);
        Assert.Contains(32, widths);
        Assert.Contains(48, widths);
        Assert.Contains(256, widths);
    }

    [Fact]
    public void LogoPngsAreSquare()
    {
        var assets = Path.Combine(FindRepositoryRoot(), "src", "Mimi.App", "Assets");

        var master = ReadPngSize(Path.Combine(assets, "mimi-logo.png"));
        Assert.Equal(master.Width, master.Height);
        Assert.True(
            master.Width >= 512,
            $"Logo master should stay high-resolution, got {master.Width}px.");

        var preview = ReadPngSize(Path.Combine(assets, "mimi-256.png"));
        Assert.Equal((256, 256), preview);
    }

    [Fact]
    public void ProjectFileWiresTheApplicationIcon()
    {
        var projectPath = Path.Combine(
            FindRepositoryRoot(), "src", "Mimi.App", "Mimi.App.csproj");
        var project = File.ReadAllText(projectPath);
        Assert.Contains(
            "<ApplicationIcon>Assets\\mimi.ico</ApplicationIcon>",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Resource Include=\"Assets\\mimi-256.png\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Resource Include=\"Assets\\mimi.ico\" />",
            project,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InAppBrandMarkUsesTheMascotLogo()
    {
        var mainWindowPath = Path.Combine(
            FindRepositoryRoot(), "src", "Mimi.App", "MainWindow.xaml");
        var mainWindow = File.ReadAllText(mainWindowPath);
        Assert.Contains(
            "pack://application:,,,/Assets/mimi-256.png",
            mainWindow,
            StringComparison.Ordinal);
        // The running window must carry its own icon so the taskbar
        // button does not depend on the shell's shortcut icon cache.
        Assert.Contains(
            "Icon=\"pack://application:,,,/Assets/mimi.ico\"",
            mainWindow,
            StringComparison.Ordinal);
    }

    private static (int Width, int Height) ReadPngSize(string path)
    {
        Assert.True(File.Exists(path), $"Missing logo PNG at {path}.");
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 24, $"PNG at {path} looks truncated.");
        // PNG signature.
        Assert.Equal(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            bytes[..8]);
        // IHDR width/height are big-endian at offsets 16 and 20.
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return (width, height);
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
