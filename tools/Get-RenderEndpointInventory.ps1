<#
.SYNOPSIS
    Inventories Windows CoreAudio render endpoints without changing any
    device, driver, default endpoint, or application state.

.DESCRIPTION
    Uses MMDeviceEnumerator directly to list render endpoints in every
    CoreAudio device state and identify the default endpoint for the
    Console, Multimedia, and Communications roles.

    This is intended as the independent precondition check for a P4-E
    zero-output acceptance run in a disposable Windows VM or secondary PC:
    run it with -ExpectedActiveCount 0 after removing the guest sound card,
    and only proceed when the script exits successfully. Run it again after
    restore to prove an active endpoint returned.

    The Windows host running Mimi should not be made endpoint-less merely
    to satisfy this check. The script itself is read-only.

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Get-RenderEndpointInventory.ps1

.EXAMPLE
    powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File .\tools\Get-RenderEndpointInventory.ps1 `
        -ExpectedActiveCount 0 `
        -JsonPath .\artifacts\acceptance\zero-output-before.json
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateRange(-1, 10000)]
    [int]$ExpectedActiveCount = -1,

    [string]$JsonPath,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this inventory with Windows PowerShell 5.1 (powershell.exe), not PowerShell Core.'
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This inventory requires Windows CoreAudio.'
}

$endpointTypeDefinition = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Mimi.Acceptance
{
    public sealed class RenderEndpointInfo
    {
        public string Id { get; set; }
        public string FriendlyName { get; set; }
        public uint State { get; set; }
        public string StateName { get; set; }
    }

    public sealed class DefaultRenderEndpointInfo
    {
        public string Role { get; set; }
        public string Id { get; set; }
        public string FriendlyName { get; set; }
        public uint State { get; set; }
        public string StateName { get; set; }
        public int HResult { get; set; }
        public string HResultHex { get; set; }
    }

    public static class RenderEndpointInventory
    {
        private const uint DeviceStateActive = 0x00000001;
        private const uint DeviceStateDisabled = 0x00000002;
        private const uint DeviceStateNotPresent = 0x00000004;
        private const uint DeviceStateUnplugged = 0x00000008;
        private const uint DeviceStateMaskAll = 0x0000000F;
        private const uint StgmRead = 0x00000000;
        private const ushort VtLpwstr = 31;

        private static readonly Guid DeviceFriendlyNameFormatId =
            new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");

        public static RenderEndpointInfo[] GetEndpoints()
        {
            IMMDeviceEnumerator enumerator = CreateEnumerator();
            IMMDeviceCollection collection = null;
            List<RenderEndpointInfo> endpoints = new List<RenderEndpointInfo>();

            try
            {
                int hr = enumerator.EnumAudioEndpoints(
                    EDataFlow.eRender,
                    DeviceStateMaskAll,
                    out collection);
                ThrowIfFailed(hr, "EnumAudioEndpoints");

                uint count;
                hr = collection.GetCount(out count);
                ThrowIfFailed(hr, "IMMDeviceCollection.GetCount");

                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        hr = collection.Item(i, out device);
                        ThrowIfFailed(hr, "IMMDeviceCollection.Item");
                        endpoints.Add(ReadEndpoint(device));
                    }
                    finally
                    {
                        Release(device);
                    }
                }

                return endpoints.ToArray();
            }
            finally
            {
                Release(collection);
                Release(enumerator);
            }
        }

        public static DefaultRenderEndpointInfo GetDefaultEndpoint(string roleName)
        {
            ERole role = (ERole)Enum.Parse(typeof(ERole), roleName);
            IMMDeviceEnumerator enumerator = CreateEnumerator();
            IMMDevice device = null;

            try
            {
                int hr = enumerator.GetDefaultAudioEndpoint(
                    EDataFlow.eRender,
                    role,
                    out device);

                DefaultRenderEndpointInfo result = new DefaultRenderEndpointInfo();
                result.Role = roleName;
                result.HResult = hr;
                result.HResultHex = FormatHResult(hr);

                if (hr == 0 && device != null)
                {
                    RenderEndpointInfo endpoint = ReadEndpoint(device);
                    result.Id = endpoint.Id;
                    result.FriendlyName = endpoint.FriendlyName;
                    result.State = endpoint.State;
                    result.StateName = endpoint.StateName;
                }

                return result;
            }
            finally
            {
                Release(device);
                Release(enumerator);
            }
        }

        private static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        }

        private static RenderEndpointInfo ReadEndpoint(IMMDevice device)
        {
            string id;
            int hr = device.GetId(out id);
            ThrowIfFailed(hr, "IMMDevice.GetId");

            uint state;
            hr = device.GetState(out state);
            ThrowIfFailed(hr, "IMMDevice.GetState");

            RenderEndpointInfo endpoint = new RenderEndpointInfo();
            endpoint.Id = id;
            endpoint.FriendlyName = GetFriendlyName(device);
            endpoint.State = state;
            endpoint.StateName = GetStateName(state);
            return endpoint;
        }

        private static string GetFriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            try
            {
                int hr = device.OpenPropertyStore(StgmRead, out store);
                if (hr != 0 || store == null)
                {
                    return String.Empty;
                }

                PropertyKey key = new PropertyKey();
                key.FormatId = DeviceFriendlyNameFormatId;
                key.PropertyId = 14;

                PropVariant value;
                hr = store.GetValue(ref key, out value);
                if (hr != 0)
                {
                    return String.Empty;
                }

                try
                {
                    if (value.VarType == VtLpwstr &&
                        value.StringPointer != IntPtr.Zero)
                    {
                        return Marshal.PtrToStringUni(value.StringPointer);
                    }

                    return String.Empty;
                }
                finally
                {
                    PropVariantClear(ref value);
                }
            }
            catch
            {
                return String.Empty;
            }
            finally
            {
                Release(store);
            }
        }

        private static string GetStateName(uint state)
        {
            List<string> names = new List<string>();
            if ((state & DeviceStateActive) != 0) names.Add("Active");
            if ((state & DeviceStateDisabled) != 0) names.Add("Disabled");
            if ((state & DeviceStateNotPresent) != 0) names.Add("NotPresent");
            if ((state & DeviceStateUnplugged) != 0) names.Add("Unplugged");
            return names.Count == 0 ? "Unknown" : String.Join("+", names.ToArray());
        }

        private static void ThrowIfFailed(int hr, string operation)
        {
            if (hr < 0)
            {
                throw new COMException(
                    operation + " failed with HRESULT " + FormatHResult(hr) + ".",
                    hr);
            }
        }

        private static string FormatHResult(int hr)
        {
            return String.Format("0x{0:X8}", unchecked((uint)hr));
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant value);
    }

    internal enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    internal enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(
            EDataFlow dataFlow,
            uint stateMask,
            out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(
            EDataFlow dataFlow,
            ERole role,
            out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            uint clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

        [PreserveSig]
        int OpenPropertyStore(
            uint access,
            out IPropertyStore properties);

        [PreserveSig]
        int GetId(
            [MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint index, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    // Native PROPVARIANT is 24 bytes on 64-bit Windows (16 on 32-bit).
    // Explicitly reserve 24 bytes so IPropertyStore.GetValue and
    // PropVariantClear can never write past this managed representation.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VarType;

        [FieldOffset(8)]
        public IntPtr StringPointer;
    }
}
'@

$inventoryTypeName = [System.Management.Automation.PSTypeName]'Mimi.Acceptance.RenderEndpointInventory'
if ($null -eq $inventoryTypeName.Type) {
    Add-Type -TypeDefinition $endpointTypeDefinition -Language CSharp
}

$endpoints = @([Mimi.Acceptance.RenderEndpointInventory]::GetEndpoints())
$defaults = @()
foreach ($role in @('eConsole', 'eMultimedia', 'eCommunications')) {
    $defaults += [Mimi.Acceptance.RenderEndpointInventory]::GetDefaultEndpoint($role)
}

$activeCount = @($endpoints | Where-Object { ($_.State -band 1) -ne 0 }).Count
$disabledCount = @($endpoints | Where-Object { ($_.State -band 2) -ne 0 }).Count
$notPresentCount = @($endpoints | Where-Object { ($_.State -band 4) -ne 0 }).Count
$unpluggedCount = @($endpoints | Where-Object { ($_.State -band 8) -ne 0 }).Count
$expectedActiveCountMatches = $null
if ($ExpectedActiveCount -ge 0) {
    $expectedActiveCountMatches = $activeCount -eq $ExpectedActiveCount
}

$result = [pscustomobject][ordered]@{
    GeneratedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    ComputerName = $env:COMPUTERNAME
    DataFlow = 'Render'
    TotalCount = $endpoints.Count
    ActiveCount = $activeCount
    DisabledCount = $disabledCount
    NotPresentCount = $notPresentCount
    UnpluggedCount = $unpluggedCount
    ExpectedActiveCount = if ($ExpectedActiveCount -ge 0) { $ExpectedActiveCount } else { $null }
    ExpectedActiveCountMatches = $expectedActiveCountMatches
    Defaults = $defaults
    Endpoints = $endpoints
}

if (-not [string]::IsNullOrWhiteSpace($JsonPath)) {
    $jsonDirectory = Split-Path -Parent $JsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDirectory)) {
        [void](New-Item -ItemType Directory -Force -Path $jsonDirectory)
    }

    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $JsonPath -Encoding UTF8
}

Write-Host 'CoreAudio render endpoints (read-only inventory):'
if ($endpoints.Count -eq 0) {
    Write-Host '(no render endpoints returned by CoreAudio)'
}
else {
    $endpoints |
        Select-Object StateName, FriendlyName, Id |
        Format-Table -AutoSize |
        Out-String |
        Write-Host
}

Write-Host 'Default render endpoints:'
$defaults |
    Select-Object Role, FriendlyName, StateName, HResultHex |
    Format-Table -AutoSize |
    Out-String |
    Write-Host

Write-Host "Counts: total=$($endpoints.Count); active=$activeCount; disabled=$disabledCount; notPresent=$notPresentCount; unplugged=$unpluggedCount"
if ($null -ne $expectedActiveCountMatches) {
    Write-Host "ExpectedActiveCount=$ExpectedActiveCount; matches=$expectedActiveCountMatches"
}

if ($PassThru) {
    $result
}

if ($expectedActiveCountMatches -eq $false) {
    exit 2
}
