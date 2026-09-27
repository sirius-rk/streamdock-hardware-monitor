namespace StreamDockHardwareMonitor.Hardware;

public enum MetricKind
{
    CpuLoad,
    MemoryUsage,
    GpuLoad,
    GpuTemperature,
    VramUsage
}

public sealed record ActionDefinition(string Uuid, string Name, MetricKind Metric)
{
    public static IReadOnlyList<ActionDefinition> All { get; } =
    [
        new("com.ziheng.streamdock.hardware-monitor.cpu", "CPU Load", MetricKind.CpuLoad),
        new("com.ziheng.streamdock.hardware-monitor.memory", "Memory Usage", MetricKind.MemoryUsage),
        new("com.ziheng.streamdock.hardware-monitor.gpu-load", "GPU Load", MetricKind.GpuLoad),
        new("com.ziheng.streamdock.hardware-monitor.gpu-temperature", "GPU Temperature", MetricKind.GpuTemperature),
        new("com.ziheng.streamdock.hardware-monitor.vram", "VRAM Usage", MetricKind.VramUsage)
    ];

    public static bool TryGet(string? uuid, out ActionDefinition definition)
    {
        definition = All.FirstOrDefault(action =>
            string.Equals(action.Uuid, uuid, StringComparison.Ordinal))!;
        return definition is not null;
    }
}
