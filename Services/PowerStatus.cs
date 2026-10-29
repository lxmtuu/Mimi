using System.Runtime.InteropServices;

namespace Mimi.Services;

/// <summary>Small Windows power-state helper used to throttle rendering.</summary>
internal static class PowerStatus
{
    public static bool IsBatterySaverEnabled =>
        TryGetStatus(out var status) && status.SystemStatusFlag != 0;

    private static bool TryGetStatus(out SystemPowerStatus status) =>
        GetSystemPowerStatus(out status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
