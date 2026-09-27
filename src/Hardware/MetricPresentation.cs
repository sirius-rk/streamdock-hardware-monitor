using System.Globalization;

namespace StreamDockHardwareMonitor.Hardware;

public enum MetricDisplayState
{
    Normal = 0,
    Warning = 1,
    Critical = 2,
    Unavailable = 3
}

public static class MetricPresentation
{
    public static string FormatTitle(MetricKind metric, double? value)
    {
        if (value is null || !double.IsFinite(value.Value))
        {
            return "--";
        }

        var rounded = Math.Round(value.Value, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
        return metric == MetricKind.GpuTemperature ? $"{rounded}°" : $"{rounded}%";
    }

    public static MetricDisplayState GetState(MetricKind metric, double? value)
    {
        if (value is null || !double.IsFinite(value.Value))
        {
            return MetricDisplayState.Unavailable;
        }

        var (warning, critical) = metric switch
        {
            MetricKind.CpuLoad => (70d, 90d),
            MetricKind.MemoryUsage => (80d, 90d),
            MetricKind.GpuLoad => (80d, 95d),
            MetricKind.GpuTemperature => (75d, 85d),
            MetricKind.VramUsage => (85d, 95d),
            _ => (100d, 100d)
        };

        return value.Value >= critical
            ? MetricDisplayState.Critical
            : value.Value >= warning
                ? MetricDisplayState.Warning
                : MetricDisplayState.Normal;
    }
}
