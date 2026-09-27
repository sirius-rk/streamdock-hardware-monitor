using StreamDockHardwareMonitor.Hardware;

var tests = new (string Name, Action Run)[]
{
    ("CPU load delta calculation", TestCpuLoad),
    ("physical memory percentage", TestMemoryUsage),
    ("NVIDIA CSV parsing", TestNvidiaOutput),
    ("NVIDIA unavailable values", TestNvidiaUnavailable),
    ("manifest action catalog", TestActionCatalog),
    ("compact title formatting", TestFormatting),
    ("metric state thresholds", TestStates)
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
    const string csv = "41, 64, 8459, 12282\r\n";
    Assert(NvidiaSmiReader.TryParseOutput(csv, out var reading), "Expected NVIDIA row to parse.");
    AssertEqual(41d, reading.GpuLoadPercent);
    AssertEqual(64d, reading.TemperatureCelsius);
    Assert(Math.Abs(reading.VramUsagePercent - 8459d * 100d / 12282d) < 0.001d,
        "VRAM percentage was not calculated correctly.");
}

static void TestNvidiaUnavailable()
{
    Assert(!NvidiaSmiReader.TryParseOutput("N/A, N/A, N/A, N/A", out _),
        "N/A values must not be treated as a valid reading.");
    Assert(!NvidiaSmiReader.TryParseOutput("41, 64, 900, 800", out _),
        "Used VRAM larger than total must be rejected.");
}

static void TestFormatting()
{
    AssertEqual("42%", MetricPresentation.FormatTitle(MetricKind.CpuLoad, 41.6));
    AssertEqual("64°", MetricPresentation.FormatTitle(MetricKind.GpuTemperature, 64));
    AssertEqual("--", MetricPresentation.FormatTitle(MetricKind.GpuLoad, null));
}

static void TestActionCatalog()
{
    AssertEqual(5, ActionDefinition.All.Count);
    Assert(ActionDefinition.TryGet("com.ziheng.streamdock.hardware-monitor.gpu-temperature", out var action),
        "Expected the GPU temperature action to be registered.");
    AssertEqual(MetricKind.GpuTemperature, action.Metric);
    Assert(!ActionDefinition.TryGet("com.example.unknown", out _),
        "Unknown actions must not be treated as this plugin's actions.");
}

static void TestStates()
{
    AssertEqual(MetricDisplayState.Normal, MetricPresentation.GetState(MetricKind.CpuLoad, 69));
    AssertEqual(MetricDisplayState.Warning, MetricPresentation.GetState(MetricKind.CpuLoad, 70));
    AssertEqual(MetricDisplayState.Critical, MetricPresentation.GetState(MetricKind.CpuLoad, 90));
    AssertEqual(MetricDisplayState.Unavailable, MetricPresentation.GetState(MetricKind.GpuLoad, null));
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
