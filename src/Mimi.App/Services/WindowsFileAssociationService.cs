using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Per-user Windows "Open with" registration. Everything is written under
/// HKEY_CURRENT_USER, so no elevation is required. Mimi registers its ProgID
/// and capabilities but deliberately never writes the protected UserChoice
/// values that choose the user's default application. The same opt-in
/// registration also maintains a per-user Start menu identity shortcut so
/// the Windows shell can resolve Mimi's AppUserModelID to its name and icon.
/// </summary>
public sealed class WindowsFileAssociationService : IFileAssociationService
{
    private const string ApplicationName = "Mimi";
    private const string ProgId = "Mimi.AudioFile";
    private const string CapabilitiesPath = @"Software\Mimi\Capabilities";
    private const string RegisteredApplicationsPath = @"Software\RegisteredApplications";
    private const string AppUserModelIdValueName = "AppUserModelId";
    private const string StartMenuShortcutTargetValueName =
        "StartMenuShortcutTarget";
    private const uint AssociationChanged = 0x08000000;
    private const uint IdListFlag = 0x0000;

    public WindowsFileAssociationService()
        : this(ResolveExecutablePath())
    {
    }

    internal WindowsFileAssociationService(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("An executable path is required.", nameof(executablePath));

        ExecutablePath = Path.GetFullPath(executablePath);
    }

    public string ExecutablePath { get; }

    private string ExecutableFileName => Path.GetFileName(ExecutablePath);

    private string OpenCommand => $"\"{ExecutablePath}\" \"%1\"";

    private string IconValue => $"\"{ExecutablePath}\",0";

    private bool IsRegistrationTarget =>
        string.Equals(
            ExecutableFileName,
            "Mimi.exe",
            StringComparison.OrdinalIgnoreCase) &&
        File.Exists(ExecutablePath);

    public bool IsRegistered
    {
        get
        {
            if (!IsRegistrationTarget) return false;

            try
            {
                if (!ValueEquals(
                        $@"Software\Classes\{ProgId}\shell\open\command",
                        string.Empty,
                        OpenCommand) ||
                    !ValueEquals(
                        $@"Software\Classes\Applications\{ExecutableFileName}\shell\open\command",
                        string.Empty,
                        OpenCommand) ||
                    !ValueEquals(
                        $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{ExecutableFileName}",
                        string.Empty,
                        ExecutablePath))
                {
                    return false;
                }

                return SupportedAudioFiles.Extensions.All(IsExtensionRegistered);
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    public void Register()
    {
        if (!IsRegistrationTarget)
        {
            throw new InvalidOperationException(
                "Start Mimi.exe before registering it with Windows Explorer.");
        }

        using var classes = CreateSubKey(Registry.CurrentUser, @"Software\Classes");

        WriteProgId(classes);
        WriteApplication(classes);
        foreach (var extension in SupportedAudioFiles.Extensions)
            WriteExtension(classes, extension);

        using (var appPath = CreateSubKey(
                   Registry.CurrentUser,
                   $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{ExecutableFileName}"))
        {
            appPath.SetValue(string.Empty, ExecutablePath);
            appPath.SetValue(
                "Path",
                Path.GetDirectoryName(ExecutablePath) ?? string.Empty);
        }

        var ownsStartMenuShortcut = ValueExists(
            CapabilitiesPath,
            StartMenuShortcutTargetValueName);
        try
        {
            var shortcutUpdate =
                WindowsAppIdentity.EnsureStartMenuShortcut(ExecutablePath);
            ownsStartMenuShortcut = ownsStartMenuShortcut ||
                shortcutUpdate ==
                WindowsAppIdentity.StartMenuShortcutUpdate.Created;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            COMException or
            DllNotFoundException or
            EntryPointNotFoundException or
            InvalidCastException or
            InvalidComObjectException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException or
            InvalidOperationException or
            System.Security.SecurityException)
        {
            // The Explorer registration above is independently useful. A
            // shortcut failure is therefore diagnostic-only here; Windows
            // may keep showing a generic source label until re-registration.
            Log.Warning(
                ex,
                "Could not create or update Mimi's Start menu identity shortcut");
        }

        WriteCapabilities(ownsStartMenuShortcut);
        NotifyAssociationsChanged();
    }

    public void Unregister()
    {
        var ownsStartMenuShortcut = ValueExists(
            CapabilitiesPath,
            StartMenuShortcutTargetValueName);

        Registry.CurrentUser.DeleteSubKeyTree(
            $@"Software\Classes\{ProgId}",
            throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(
            $@"Software\Classes\Applications\{ExecutableFileName}",
            throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(
            $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{ExecutableFileName}",
            throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(
            CapabilitiesPath,
            throwOnMissingSubKey: false);

        using (var registeredApplications = Registry.CurrentUser.OpenSubKey(
                   RegisteredApplicationsPath,
                   writable: true))
        {
            if (registeredApplications?.GetValue(ApplicationName) as string is { } target &&
                string.Equals(target, CapabilitiesPath, StringComparison.OrdinalIgnoreCase))
            {
                registeredApplications.DeleteValue(
                    ApplicationName,
                    throwOnMissingValue: false);
            }
        }

        foreach (var extension in SupportedAudioFiles.Extensions)
        {
            DeleteValue(
                $@"Software\Classes\{extension}\OpenWithProgids",
                ProgId);
            DeleteValue(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids",
                ProgId);
        }

        if (ownsStartMenuShortcut)
        {
            try
            {
                _ = WindowsAppIdentity.RemoveStartMenuShortcut(
                    ExecutablePath);
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                COMException or
                InvalidCastException or
                InvalidComObjectException or
                IOException or
                NotSupportedException or
                UnauthorizedAccessException or
                InvalidOperationException or
                System.Security.SecurityException)
            {
                Log.Warning(
                    ex,
                    "Could not remove Mimi's Start menu identity shortcut");
            }
        }

        NotifyAssociationsChanged();
    }

    private void WriteProgId(RegistryKey classes)
    {
        using var progId = CreateSubKey(classes, ProgId);
        progId.SetValue(string.Empty, "Mimi audio file");
        progId.SetValue("FriendlyTypeName", "Mimi audio file");

        using (var icon = CreateSubKey(progId, "DefaultIcon"))
            icon.SetValue(string.Empty, IconValue);

        using var open = CreateSubKey(progId, @"shell\open");
        open.SetValue(string.Empty, "Open with Mimi");
        open.SetValue("MUIVerb", "Open with Mimi");

        using var command = CreateSubKey(open, "command");
        command.SetValue(string.Empty, OpenCommand);
    }

    private void WriteApplication(RegistryKey classes)
    {
        using var application = CreateSubKey(
            classes,
            $@"Applications\{ExecutableFileName}");
        application.SetValue("FriendlyAppName", ApplicationName);

        using (var icon = CreateSubKey(application, "DefaultIcon"))
            icon.SetValue(string.Empty, IconValue);

        using (var command = CreateSubKey(application, @"shell\open\command"))
            command.SetValue(string.Empty, OpenCommand);

        using var supportedTypes = CreateSubKey(application, "SupportedTypes");
        foreach (var extension in SupportedAudioFiles.Extensions)
            supportedTypes.SetValue(extension, string.Empty);
    }

    private void WriteExtension(RegistryKey classes, string extension)
    {
        using (var classesProgIds = CreateSubKey(
                   classes,
                   $@"{extension}\OpenWithProgids"))
        {
            classesProgIds.SetValue(
                ProgId,
                Array.Empty<byte>(),
                RegistryValueKind.None);
        }

        using var explorerProgIds = CreateSubKey(
            Registry.CurrentUser,
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids");
        explorerProgIds.SetValue(
            ProgId,
            Array.Empty<byte>(),
            RegistryValueKind.None);
    }

    private void WriteCapabilities(bool ownsStartMenuShortcut)
    {
        using var capabilities = CreateSubKey(
            Registry.CurrentUser,
            CapabilitiesPath);
        capabilities.SetValue("ApplicationName", ApplicationName);
        capabilities.SetValue(
            "ApplicationDescription",
            "Mimi is a desktop music player with a local library, playlists, and a reactive Stage visualizer.");
        capabilities.SetValue("ApplicationIcon", IconValue);
        capabilities.SetValue(
            AppUserModelIdValueName,
            WindowsAppIdentity.AppUserModelId);
        if (ownsStartMenuShortcut)
        {
            capabilities.SetValue(
                StartMenuShortcutTargetValueName,
                ExecutablePath);
        }
        else
        {
            capabilities.DeleteValue(
                StartMenuShortcutTargetValueName,
                throwOnMissingValue: false);
        }

        using (var fileAssociations = CreateSubKey(
                   capabilities,
                   "FileAssociations"))
        {
            foreach (var extension in SupportedAudioFiles.Extensions)
                fileAssociations.SetValue(extension, ProgId);
        }

        using var registeredApplications = CreateSubKey(
            Registry.CurrentUser,
            RegisteredApplicationsPath);
        registeredApplications.SetValue(ApplicationName, CapabilitiesPath);
    }

    private bool IsExtensionRegistered(string extension) =>
        ValueExists(
            $@"Software\Classes\{extension}\OpenWithProgids",
            ProgId) &&
        ValueExists(
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids",
            ProgId) &&
        ValueExists(
            $@"Software\Classes\Applications\{ExecutableFileName}\SupportedTypes",
            extension) &&
        ValueEquals(
            $@"{CapabilitiesPath}\FileAssociations",
            extension,
            ProgId);

    private static bool ValueExists(string keyPath, string valueName) =>
        Registry.GetValue(
            $@"HKEY_CURRENT_USER\{keyPath}",
            valueName,
            null) is not null;

    private static bool ValueEquals(
        string keyPath,
        string valueName,
        string expected) =>
        Registry.GetValue(
            $@"HKEY_CURRENT_USER\{keyPath}",
            valueName,
            null) is string actual &&
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static void DeleteValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static RegistryKey CreateSubKey(RegistryKey parent, string subkeyPath) =>
        parent.CreateSubKey(subkeyPath) ??
        throw new InvalidOperationException(
            $"Windows did not allow Mimi to create the registry key '{subkeyPath}'.");

    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            string.Equals(
                Path.GetFileName(processPath),
                "Mimi.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(processPath);
        }

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var candidate = Path.ChangeExtension(assemblyPath, ".exe");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return !string.IsNullOrWhiteSpace(processPath)
            ? Path.GetFullPath(processPath)
            : Path.Combine(AppContext.BaseDirectory, "Mimi.exe");
    }

    private static void NotifyAssociationsChanged()
    {
        try
        {
            SHChangeNotify(AssociationChanged, IdListFlag, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex) when (
            ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Registration itself succeeded; Explorer will eventually refresh
            // its association cache without this best-effort notification.
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        uint wEventId,
        uint uFlags,
        IntPtr dwItem1,
        IntPtr dwItem2);
}
