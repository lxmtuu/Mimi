using System.IO;
using System.Runtime.InteropServices;
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

    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

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

            var shortcutExists = File.Exists(shortcutPath);
            string? existingTargetPath = null;
            string? existingAppUserModelId = null;

            if (shortcutExists)
            {
                persistFile.Load(shortcutPath, StorageRead);
                existingTargetPath = GetShortcutTargetPath(link);
                existingAppUserModelId =
                    GetShortcutAppUserModelId(propertyStore);

                if (PathsEqual(existingTargetPath, targetPath) &&
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
                link.SetIconLocation(targetPath, 0);
                link.SetDescription(DisplayName);
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
