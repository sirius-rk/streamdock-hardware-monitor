namespace StreamDockHardwareMonitor.Hardware;

public sealed class HardwareMetricsReader
{
    private readonly WindowsSystemMetricsReader _systemReader = new();
    private readonly NvidiaSmiReader _nvidiaReader = new();

    public HardwareSnapshot Capture()
    {
        var cpu = _systemReader.ReadCpuLoadPercent();
        var memory = _systemReader.ReadMemoryUsagePercent();
        var gpu = _nvidiaReader.Read();

        return new HardwareSnapshot(
            cpu,
            memory,
            gpu?.GpuLoadPercent,
            gpu?.TemperatureCelsius,
            gpu?.VramUsagePercent);
    }
}
