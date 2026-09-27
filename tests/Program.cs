using StreamDockHardwareMonitor.Hardware;
using System.Text;
using System.Xml.Linq;

var tests = new (string Name, Action Run)[]
{
    ("CPU load delta calculation", TestCpuLoad),
    ("physical memory percentage", TestMemoryUsage),
    ("NVIDIA CSV parsing", TestNvidiaOutput),
    ("optional NVIDIA memory temperature", TestVramTemperatureParsing),
    ("NVIDIA unavailable values", TestNvidiaUnavailable),
    ("manifest action catalog", TestActionCatalog),
    ("compact title formatting", TestFormatting),
    ("metric state thresholds", TestStates),
    ("continuous gauge and thermometer artwork", TestDynamicArtwork)
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static void TestCpuLoad()
{
    var previous = new SystemTimes(Idle: 800, Kernel: 1_000, User: 500);
    var current = new SystemTimes(Idle: 850, Kernel: 1_100, User: 600);
    AssertEqual(75d, WindowsSystemMetricsReader.CalculateCpuLoadPercent(previous, current));
    AssertNull(WindowsSystemMetricsReader.CalculateCpuLoadPercent(current, previous));
}

static void TestMemoryUsage()
{
    AssertEqual(75d, WindowsSystemMetricsReader.CalculateMemoryUsagePercent(32, 8));
    AssertNull(WindowsSystemMetricsReader.CalculateMemoryUsagePercent(0, 0));
    AssertNull(WindowsSystemMetricsReader.CalculateMemoryUsagePercent(8, 9));
}

static void TestNvidiaOutput()
{
    const string csv = "41, 64, N/A, 8459, 12282\r\n";
    Assert(NvidiaSmiReader.TryParseOutput(csv, out var reading), "Expected NVIDIA row to parse.");
    AssertEqual(41d, reading.GpuLoadPercent);
    AssertEqual(64d, reading.TemperatureCelsius);
    AssertNull(reading.VramTemperatureCelsius);
    Assert(Math.Abs(reading.VramUsagePercent - 8459d * 100d / 12282d) < 0.001d,
        "VRAM percentage was not calculated correctly.");
}

static void TestVramTemperatureParsing()
{
    Assert(NvidiaSmiReader.TryParseOutput("45, 52, 84, 3200, 12282", out var reading),
        "Expected NVIDIA row with memory temperature to parse.");
    AssertEqual(84d, reading.VramTemperatureCelsius);
}

static void TestNvidiaUnavailable()
{
    Assert(!NvidiaSmiReader.TryParseOutput("N/A, N/A, N/A, N/A, N/A", out _),
        "N/A values must not be treated as a valid reading.");
    Assert(!NvidiaSmiReader.TryParseOutput("41, 64, N/A, 900, 800", out _),
        "Used VRAM larger than total must be rejected.");
}

static void TestFormatting()
{
    AssertEqual("42%", MetricPresentation.FormatTitle(MetricKind.CpuLoad, 41.6));
    AssertEqual("64°C", MetricPresentation.FormatTitle(MetricKind.GpuTemperature, 64));
    AssertEqual("84°C", MetricPresentation.FormatTitle(MetricKind.VramTemperature, 84));
    AssertEqual("--", MetricPresentation.FormatTitle(MetricKind.GpuLoad, null));
}

static void TestActionCatalog()
{
    AssertEqual(6, ActionDefinition.All.Count);
    Assert(ActionDefinition.TryGet("com.ziheng.streamdock.hardware-monitor.gpu-temperature", out var action),
        "Expected the GPU temperature action to be registered.");
    AssertEqual(MetricKind.GpuTemperature, action.Metric);
    Assert(ActionDefinition.TryGet("com.ziheng.streamdock.hardware-monitor.vram-temperature", out var vramAction),
        "Expected the VRAM temperature action to be registered.");
    AssertEqual(MetricKind.VramTemperature, vramAction.Metric);
    Assert(!ActionDefinition.TryGet("com.example.unknown", out _),
        "Unknown actions must not be treated as this plugin's actions.");
}

static void TestStates()
{
    AssertEqual(MetricDisplayState.Normal, MetricPresentation.GetState(MetricKind.CpuLoad, 69));
    AssertEqual(MetricDisplayState.Warning, MetricPresentation.GetState(MetricKind.CpuLoad, 70));
    AssertEqual(MetricDisplayState.Critical, MetricPresentation.GetState(MetricKind.CpuLoad, 90));
    AssertEqual(MetricDisplayState.Warning, MetricPresentation.GetState(MetricKind.VramTemperature, 85));
    AssertEqual(MetricDisplayState.Critical, MetricPresentation.GetState(MetricKind.VramTemperature, 100));
    AssertEqual(MetricDisplayState.Unavailable, MetricPresentation.GetState(MetricKind.GpuLoad, null));
}

static void TestDynamicArtwork()
{
    const string prefix = "data:image/svg+xml;base64,";
    var percentDataUri = MetricIconRenderer.RenderDataUri(MetricKind.MemoryUsage, 50);
    Assert(percentDataUri.StartsWith(prefix, StringComparison.Ordinal), "Expected an SVG data URI.");
    var percentSvg = Encoding.UTF8.GetString(Convert.FromBase64String(percentDataUri[prefix.Length..]));
    _ = XDocument.Parse(percentSvg);
    Assert(percentSvg.Contains("stroke-dashoffset=\"78.54\"", StringComparison.Ordinal),
        "The percentage gauge should show half progress at 50%.");
    Assert(percentSvg.Contains(">RAM</text>", StringComparison.Ordinal),
        "The percentage gauge should identify the metric.");

    var temperatureUri = MetricIconRenderer.RenderDataUri(MetricKind.VramTemperature, 55);
    var temperatureSvg = Encoding.UTF8.GetString(Convert.FromBase64String(temperatureUri[prefix.Length..]));
    _ = XDocument.Parse(temperatureSvg);
    Assert(temperatureSvg.Contains(">VRAM</text>", StringComparison.Ordinal),
        "The thermometer should identify VRAM temperature.");
    Assert(temperatureSvg.Contains("height=\"11\"", StringComparison.Ordinal),
        "The thermometer fill should correspond to its temperature.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertNull(double? value)
{
    if (value is not null)
    {
        throw new InvalidOperationException($"Expected null, received {value}.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
    }
}
