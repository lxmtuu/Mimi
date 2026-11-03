using System.Collections;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Serilog;

namespace Mimi.Services;

/// <summary>
/// Gives the unpackaged executable a stable Windows shell identity. The
/// process-level AppUserModelID lets Windows group Mimi's windows and media
/// session together; an explicitly registered Start menu shortcut carrying
/// the same ID gives the shell a display name and icon to resolve for places
/// such as Quick Settings. A future packaged build keeps its package
/// identity instead of using this unpackaged identity.
/// </summary>
internal static class WindowsAppIdentity
{
    public enum StartMenuShortcutUpdate
    {
        AlreadyCurrent,
        Created,
        Updated
    }

    public const string AppUserModelId = "Mimi.App";
    public const string DisplayName = "Mimi";

    private const int AppModelErrorNoPackage = 15700;
    private const uint StorageRead = 0x00000000;

    private static readonly Guid AppUserModelPropertyFormatId = new(
        "9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly Guid PropertyStoreInterfaceId = new(
        "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey RelaunchCommandKey = new(
        AppUserModelPropertyFormatId,
        2);
    private static readonly PropertyKey RelaunchIconResourceKey = new(
        AppUserModelPropertyFormatId,
        3);
    private static readonly PropertyKey RelaunchDisplayNameResourceKey = new(
        AppUserModelPropertyFormatId,
        4);
    private static readonly PropertyKey AppUserModelIdKey = new(
        AppUserModelPropertyFormatId,
        5);
    private static readonly object ShellIconLock = new();
    private static string? shellIconPath;

    /// <summary>
    /// Sets this process's explicit AppUserModelID. Call this before any
    /// window, taskbar button, or SMTC session is created. Identity is a
    /// shell-integration aid, so failure is logged and never blocks startup.
    /// </summary>
    public static void TrySetProcessAppUserModelId()
    {
        try
        {
            if (HasPackageIdentity())
            {
                Log.Debug(
                    "Packaged Windows identity detected; the unpackaged " +
                    "AppUserModelID was not applied");
                return;
            }

            var result = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            if (result >= 0)
            {
                Log.Information(
                    "Windows app identity: {AppUserModelId}",
                    AppUserModelId);
            }
            else
            {
                Log.Warning(
                    "Could not set Windows app identity {AppUserModelId}; " +
                    "HRESULT={HResult}",
                    AppUserModelId,
                    $"0x{result:X8}");
            }
        }
        catch (Exception ex) when (
            ex is COMException or
            DllNotFoundException or
            EntryPointNotFoundException)
        {
            Log.Warning(ex, "Could not set the Windows app identity");
        }
    }

    /// <summary>
    /// Materializes the bundled application icon as a standalone file for
    /// Windows Shell surfaces that do not reliably follow the window icon.
    /// The file name contains a short content hash: a future icon receives
    /// a new path instead of depending on Explorer invalidating an icon
    /// cache entry for an old path.
    /// </summary>
    public static string? TryGetShellIconPath()
    {
        lock (ShellIconLock)
        {
            if (shellIconPath is not null && File.Exists(shellIconPath))
                return shellIconPath;

            try
            {
                var iconBytes = ReadBundledIconBytes();
                var localAppDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                if (iconBytes is null ||
                    string.IsNullOrWhiteSpace(localAppDataPath))
                {
                    return null;
                }

                var iconDirectory = Path.Combine(
                    localAppDataPath,
                    "Mimi",
                    "icons");
                Directory.CreateDirectory(iconDirectory);

                var iconVersion = Convert.ToHexString(
                    SHA256.HashData(iconBytes))[..12].ToLowerInvariant();
                var iconPath = Path.Combine(
                    iconDirectory,
                    $"mimi-{iconVersion}.ico");
                if (!File.Exists(iconPath) ||
                    new FileInfo(iconPath).Length != iconBytes.Length)
                {
                    File.WriteAllBytes(iconPath, iconBytes);
                }

                shellIconPath = iconPath;
                return iconPath;
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                BadImageFormatException or
                FileNotFoundException or
                IOException or
                InvalidOperationException or
                MissingManifestResourceException or
                NotSupportedException or
                UnauthorizedAccessException)
            {
                Log.Warning(ex, "Could not prepare Mimi's Windows shell icon");
                return null;
            }
        }
    }

    /// <summary>
    /// Gives the main window the same explicit AppUserModel identity as the
    /// process, plus the relaunch command/icon/name Windows uses for the
    /// taskbar group. Setting only the WPF window icon is not sufficient for
    /// an explicitly identified unpackaged app: the shell can keep using the
    /// icon stored on the matching shortcut instead.
    /// </summary>
    public static void TrySetWindowAppUserModelProperties(
        IntPtr windowHandle)
    {
        var executablePath = TryResolveExecutablePath();
        var iconPath = TryGetShellIconPath();
        if (executablePath is null || iconPath is null) return;

        TryUseWindowPropertyStore(
            windowHandle,
            "set",
            propertyStore =>
            {
                SetPropertyString(
                    propertyStore,
                    AppUserModelIdKey,
                    AppUserModelId);
                SetPropertyString(
                    propertyStore,
                    RelaunchCommandKey,
                    $"\"{executablePath}\"");
                SetPropertyString(
                    propertyStore,
                    RelaunchIconResourceKey,
                    $"{iconPath},0");
                SetPropertyString(
                    propertyStore,
                    RelaunchDisplayNameResourceKey,
                    DisplayName);
                propertyStore.Commit();

                Log.Debug(
                    "Window AppUserModel relaunch icon set to {IconPath}",
                    iconPath);
            });
    }

    /// <summary>
    /// Removes the window properties set by
    /// <see cref="TrySetWindowAppUserModelProperties"/>. Windows asks apps
    /// to clear values they put in a window property store before the HWND
    /// is destroyed.
    /// </summary>
    public static void TryClearWindowAppUserModelProperties(
        IntPtr windowHandle)
    {
        TryUseWindowPropertyStore(
            windowHandle,
            "clear",
            propertyStore =>
            {
                ClearProperty(propertyStore, AppUserModelIdKey);
                ClearProperty(propertyStore, RelaunchCommandKey);
                ClearProperty(propertyStore, RelaunchIconResourceKey);
                ClearProperty(propertyStore, RelaunchDisplayNameResourceKey);
                propertyStore.Commit();
            });
    }

    /// <summary>
    /// Creates or updates the current user's Start menu shortcut so it
    /// points at <paramref name="executablePath"/> and carries Mimi's
    /// AppUserModelID. An unrelated shortcut that merely happens to be named
    /// Mimi is left untouched.
    /// </summary>
    /// <returns>
    /// Whether the shortcut was already current, newly created, or an
    /// existing shortcut was updated. Callers can use that distinction to
    /// avoid claiming ownership of (and later deleting) a shortcut that
    /// already existed before Mimi stamped its identity onto it.
    /// </returns>
    public static StartMenuShortcutUpdate EnsureStartMenuShortcut(
        string executablePath)
    {
        var targetPath = NormalizeExecutablePath(executablePath);
        if (HasPackageIdentity())
            return StartMenuShortcutUpdate.AlreadyCurrent;

        var shortcutPath = GetStartMenuShortcutPath() ??
            throw new InvalidOperationException(
                "Windows did not provide a per-user Start menu folder.");

        object? linkObject = null;
        try
        {
            linkObject = new ShellLink();
            var link = (IShellLinkW)linkObject;
            var persistFile = (IPersistFile)linkObject;
            var propertyStore = (IPropertyStore)linkObject;

            var desiredIconPath = TryGetShellIconPath() ?? targetPath;
            var shortcutExists = File.Exists(shortcutPath);
            string? existingTargetPath = null;
            string? existingAppUserModelId = null;
            string? existingIconPath = null;
            var existingIconIndex = 0;

            if (shortcutExists)
            {
                persistFile.Load(shortcutPath, StorageRead);
                existingTargetPath = GetShortcutTargetPath(link);
                (existingIconPath, existingIconIndex) =
                    GetShortcutIconLocation(link);
                existingAppUserModelId =
                    GetShortcutAppUserModelId(propertyStore);

                if (PathsEqual(existingTargetPath, targetPath) &&
                    PathsEqual(existingIconPath, desiredIconPath) &&
                    existingIconIndex == 0 &&
                    string.Equals(
                        existingAppUserModelId,
                        AppUserModelId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return StartMenuShortcutUpdate.AlreadyCurrent;
                }

                if (!PathsEqual(existingTargetPath, targetPath) &&
                    !string.Equals(
                        existingAppUserModelId,
                        AppUserModelId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "A Start menu shortcut named Mimi already points " +
                        "to another program. Mimi left it unchanged.");
                }
            }

            if (!shortcutExists ||
                !PathsEqual(existingTargetPath, targetPath))
            {
                link.SetPath(targetPath);
                link.SetArguments(string.Empty);
                link.SetWorkingDirectory(
                    Path.GetDirectoryName(targetPath) ?? string.Empty);
                link.SetIconLocation(desiredIconPath, 0);
                link.SetDescription(DisplayName);
            }
            else if (!PathsEqual(existingIconPath, desiredIconPath) ||
                     existingIconIndex != 0)
            {
                link.SetIconLocation(desiredIconPath, 0);
            }

            SetShortcutAppUserModelId(propertyStore);

            var shortcutDirectory = Path.GetDirectoryName(shortcutPath) ??
                throw new InvalidOperationException(
                    "Windows did not provide a Start menu shortcut path.");
            Directory.CreateDirectory(shortcutDirectory);
            persistFile.Save(shortcutPath, true);

            Log.Information(
                "Windows Start menu identity shortcut {Action} for " +
                "{AppUserModelId}",
                shortcutExists ? "updated" : "created",
                AppUserModelId);
            return shortcutExists
                ? StartMenuShortcutUpdate.Updated
                : StartMenuShortcutUpdate.Created;
        }
        finally
        {
            if (linkObject is not null)
                _ = Marshal.ReleaseComObject(linkObject);
        }
    }

    public static bool IsStartMenuShortcutCurrent(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        if (GetStartMenuShortcutPath() is not { } shortcutPath ||
            !File.Exists(shortcutPath))
        {
            return false;
        }

        object? linkObject = null;
        try
        {
            linkObject = new ShellLink();
            var link = (IShellLinkW)linkObject;
            var persistFile = (IPersistFile)linkObject;
            var propertyStore = (IPropertyStore)linkObject;

            persistFile.Load(shortcutPath, StorageRead);
            return PathsEqual(
                       GetShortcutTargetPath(link),
                       Path.GetFullPath(executablePath)) &&
                   string.Equals(
                       GetShortcutAppUserModelId(propertyStore),
                       AppUserModelId,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            COMException or
            InvalidCastException or
            InvalidComObjectException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            Log.Debug(
                ex,
                "Could not inspect the Windows Start menu identity shortcut");
            return false;
        }
        finally
        {
            if (linkObject is not null)
                _ = Marshal.ReleaseComObject(linkObject);
        }
    }

    /// <summary>
    /// Removes the Start menu shortcut only when it still points at this
    /// executable and carries Mimi's AppUserModelID.
    /// </summary>
    public static bool RemoveStartMenuShortcut(string executablePath)
    {
        if (GetStartMenuShortcutPath() is not { } shortcutPath ||
            !File.Exists(shortcutPath) ||
            !IsStartMenuShortcutCurrent(executablePath))
        {
            return false;
        }

        File.Delete(shortcutPath);
        Log.Information(
            "Windows Start menu identity shortcut removed for " +
            "{AppUserModelId}",
            AppUserModelId);
        return true;
    }

    private static string NormalizeExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException(
                "An executable path is required.",
                nameof(executablePath));
        }

        var targetPath = Path.GetFullPath(executablePath);
        if (!string.Equals(
                Path.GetFileName(targetPath),
                "Mimi.exe",
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(targetPath))
        {
            throw new InvalidOperationException(
                "Start Mimi.exe before creating its Start menu shortcut.");
        }

        return targetPath;
    }

    private static string? GetStartMenuShortcutPath()
    {
        var programsFolder = Environment.GetFolderPath(
            Environment.SpecialFolder.Programs);
        return string.IsNullOrWhiteSpace(programsFolder)
            ? null
            : Path.Combine(programsFolder, "Mimi.lnk");
    }

    private static byte[]? ReadBundledIconBytes()
    {
        var assembly = typeof(WindowsAppIdentity).Assembly;
        var resourceStreamName = assembly.GetManifestResourceNames()
            .FirstOrDefault(static name => name.EndsWith(
                ".g.resources",
                StringComparison.OrdinalIgnoreCase));
        if (resourceStreamName is null) return null;

        using var resourceStream =
            assembly.GetManifestResourceStream(resourceStreamName);
        if (resourceStream is null) return null;

        using var reader = new ResourceReader(resourceStream);
        foreach (DictionaryEntry entry in reader)
        {
            if (entry.Key is not string key ||
                !key.Equals(
                    "assets/mimi.ico",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return entry.Value switch
            {
                byte[] bytes => bytes,
                Stream stream => ReadAllBytes(stream),
                _ => null
            };
        }

        return null;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string? TryResolveExecutablePath()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) &&
                string.Equals(
                    Path.GetFileName(processPath),
                    "Mimi.exe",
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(processPath))
            {
                return Path.GetFullPath(processPath);
            }

            var assemblyPath = typeof(WindowsAppIdentity).Assembly.Location;
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                var candidate = Path.ChangeExtension(assemblyPath, ".exe");
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            Log.Debug(ex, "Could not resolve Mimi's executable path");
        }

        return null;
    }

    private static void TryUseWindowPropertyStore(
        IntPtr windowHandle,
        string operation,
        Action<IPropertyStore> usePropertyStore)
    {
        if (windowHandle == IntPtr.Zero) return;

        object? propertyStoreObject = null;
        try
        {
            if (HasPackageIdentity()) return;

            var interfaceId = PropertyStoreInterfaceId;
            var result = SHGetPropertyStoreForWindow(
                windowHandle,
                ref interfaceId,
                out var propertyStorePointer);
            if (result < 0 || propertyStorePointer == IntPtr.Zero)
            {
                Log.Warning(
                    "Could not {Operation} Mimi's window AppUserModel " +
                    "properties; HRESULT={HResult}",
                    operation,
                    $"0x{result:X8}");
                return;
            }

            try
            {
                propertyStoreObject =
                    Marshal.GetObjectForIUnknown(propertyStorePointer);
            }
            finally
            {
                _ = Marshal.Release(propertyStorePointer);
            }

            usePropertyStore((IPropertyStore)propertyStoreObject);
        }
        catch (Exception ex) when (
            ex is COMException or
            DllNotFoundException or
            EntryPointNotFoundException or
            InvalidCastException or
            InvalidComObjectException or
            InvalidOperationException or
            MarshalDirectiveException or
            NotSupportedException)
        {
            Log.Warning(
                ex,
                "Could not {Operation} Mimi's window AppUserModel properties",
                operation);
        }
        finally
        {
            if (propertyStoreObject is not null)
                _ = Marshal.ReleaseComObject(propertyStoreObject);
        }
    }

    private static void SetPropertyString(
        IPropertyStore propertyStore,
        PropertyKey key,
        string value)
    {
        var propertyValue = PropVariant.FromString(value);
        try
        {
            propertyStore.SetValue(ref key, ref propertyValue);
        }
        finally
        {
            _ = PropVariantClear(ref propertyValue);
        }
    }

    private static void ClearProperty(
        IPropertyStore propertyStore,
        PropertyKey key)
    {
        var emptyValue = new PropVariant();
        propertyStore.SetValue(ref key, ref emptyValue);
    }

    private static bool HasPackageIdentity()
    {
        var packageFullNameLength = 0;
        return GetCurrentPackageFullName(
                ref packageFullNameLength,
                null) !=
            AppModelErrorNoPackage;
    }

    private static string? GetShortcutTargetPath(IShellLinkW link)
    {
        var buffer = new char[4096];
        link.GetPath(buffer, buffer.Length, IntPtr.Zero, 0);

        var length = Array.IndexOf(buffer, '\0');
        if (length < 0) length = buffer.Length;

        var path = new string(buffer, 0, length).Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static (string? Path, int Index) GetShortcutIconLocation(
        IShellLinkW link)
    {
        var buffer = new char[4096];
        link.GetIconLocation(buffer, buffer.Length, out var index);

        var length = Array.IndexOf(buffer, '\0');
        if (length < 0) length = buffer.Length;

        var path = new string(buffer, 0, length).Trim();
        return (string.IsNullOrWhiteSpace(path) ? null : path, index);
    }

    private static string? GetShortcutAppUserModelId(
        IPropertyStore propertyStore)
    {
        var key = AppUserModelIdKey;
        propertyStore.GetValue(ref key, out var value);
        try
        {
            return value.ToStringValue();
        }
        finally
        {
            _ = PropVariantClear(ref value);
        }
    }

    private static void SetShortcutAppUserModelId(
        IPropertyStore propertyStore)
    {
        var key = AppUserModelIdKey;
        var value = PropVariant.FromString(AppUserModelId);
        try
        {
            propertyStore.SetValue(ref key, ref value);
            propertyStore.Commit();
        }
        finally
        {
            _ = PropVariantClear(ref value);
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) ||
            string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            IOException or
            NotSupportedException)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode,
        ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        string appId);

    [DllImport(
        "shell32.dll",
        ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr windowHandle,
        ref Guid interfaceId,
        out IntPtr propertyStore);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetCurrentPackageFullName(
        ref int packageFullNameLength,
        char[]? packageFullName);

    [DllImport("ole32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }
    }

    /// <summary>
    /// The subset of PROPVARIANT needed for one VT_LPWSTR value. Sequential
    /// layout gives the native 16-byte shape on 32-bit Windows and 24-byte
    /// shape on 64-bit Windows.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        private const ushort VtLpwstr = 31;

        public ushort VarType;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Pointer;
        public int PointerHigh;

        public static PropVariant FromString(string value) => new()
        {
            VarType = VtLpwstr,
            Pointer = Marshal.StringToCoTaskMemUni(value)
        };

        public readonly string? ToStringValue() =>
            VarType == VtLpwstr && Pointer != IntPtr.Zero
                ? Marshal.PtrToStringUni(Pointer)
                : null;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private class ShellLink
    {
    }

    /// <summary>IShellLinkW, declared in native vtable order.</summary>
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1)] char[] file,
            int size,
            IntPtr findData,
            uint flags);

        void GetIDList(out IntPtr idList);

        void SetIDList(IntPtr idList);

        void GetDescription(
            [Out, MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1)] char[] name,
            int size);

        void SetDescription(
            [MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory(
            [Out, MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1)] char[] directory,
            int size);

        void SetWorkingDirectory(
            [MarshalAs(UnmanagedType.LPWStr)] string directory);

        void GetArguments(
            [Out, MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1)] char[] arguments,
            int size);

        void SetArguments(
            [MarshalAs(UnmanagedType.LPWStr)] string arguments);

        void GetHotkey(out ushort hotkey);

        void SetHotkey(ushort hotkey);

        void GetShowCmd(out int showCommand);

        void SetShowCmd(int showCommand);

        void GetIconLocation(
            [Out, MarshalAs(
                UnmanagedType.LPArray,
                SizeParamIndex = 1)] char[] iconPath,
            int size,
            out int index);

        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)] string iconPath,
            int index);

        void SetRelativePath(
            [MarshalAs(UnmanagedType.LPWStr)] string relativePath,
            uint reserved);

        void Resolve(IntPtr window, uint flags);

        void SetPath(
            [MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    /// <summary>IPersistFile, declared in native vtable order.</summary>
    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);

        [PreserveSig]
        int IsDirty();

        void Load(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            uint mode);

        void Save(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            [MarshalAs(UnmanagedType.Bool)] bool remember);

        void SaveCompleted(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName);

        void GetCurFile(out IntPtr fileName);
    }

    /// <summary>IPropertyStore, declared in native vtable order.</summary>
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);

        void GetAt(uint index, out PropertyKey key);

        void GetValue(ref PropertyKey key, out PropVariant value);

        void SetValue(ref PropertyKey key, ref PropVariant value);

        void Commit();
    }
}
