using System.Runtime.InteropServices;

namespace StreamDockHardwareMonitor.Hardware;

public sealed class WindowsSystemMetricsReader
{
    private SystemTimes? _previousTimes;

    public double? ReadCpuLoadPercent()
    {
        if (!OperatingSystem.IsWindows()
            || !GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        var current = new SystemTimes(idle.ToUInt64(), kernel.ToUInt64(), user.ToUInt64());
        var previous = _previousTimes;
        _previousTimes = current;

        return previous is null
            ? null
            : CalculateCpuLoadPercent(previous.Value, current);
    }

    public double? ReadMemoryUsagePercent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusEx(ref status)
            ? CalculateMemoryUsagePercent(status.TotalPhysical, status.AvailablePhysical)
            : null;
    }

    public static double? CalculateCpuLoadPercent(SystemTimes previous, SystemTimes current)
    {
        var previousTotal = previous.Kernel + previous.User;
        var currentTotal = current.Kernel + current.User;
        if (currentTotal <= previousTotal || current.Idle < previous.Idle)
        {
            return null;
        }

        var totalDelta = currentTotal - previousTotal;
        var idleDelta = current.Idle - previous.Idle;
        if (idleDelta > totalDelta)
        {
            return null;
        }

        return ClampPercent((totalDelta - idleDelta) * 100d / totalDelta);
    }

    public static double? CalculateMemoryUsagePercent(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0 || availableBytes > totalBytes)
        {
            return null;
        }

        return ClampPercent((totalBytes - availableBytes) * 100d / totalBytes);
    }

    private static double ClampPercent(double value) => Math.Clamp(value, 0d, 100d);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly ulong ToUInt64() =>
            ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}

public readonly record struct SystemTimes(ulong Idle, ulong Kernel, ulong User);
