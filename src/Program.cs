using System.Text.Json;
using StreamDockHardwareMonitor.Hardware;
using StreamDockHardwareMonitor.Plugin;

if (args.Length == 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase))
{
    using var reader = new HardwareMetricsReader();
    _ = reader.Capture();
    await Task.Delay(TimeSpan.FromSeconds(1));
    var snapshot = reader.Capture();
    Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

return await PluginHost.RunAsync(args);
