namespace StreamDockHardwareMonitor.Hardware;

public sealed record HardwareSnapshot(
    double? CpuLoadPercent,
    double? MemoryUsagePercent,
    double? GpuLoadPercent,
    double? GpuTemperatureCelsius,
    double? VramUsagePercent)
{
    public double? Get(MetricKind metric) => metric switch
    {
        MetricKind.CpuLoad => CpuLoadPercent,
        MetricKind.MemoryUsage => MemoryUsagePercent,
        MetricKind.GpuLoad => GpuLoadPercent,
        MetricKind.GpuTemperature => GpuTemperatureCelsius,
        MetricKind.VramUsage => VramUsagePercent,
        _ => null
    };
}

public sealed record NvidiaGpuReading(
    double GpuLoadPercent,
    double TemperatureCelsius,
    double VramUsagePercent);
