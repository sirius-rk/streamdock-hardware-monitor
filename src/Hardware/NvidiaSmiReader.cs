using System.Diagnostics;
using System.Globalization;

namespace StreamDockHardwareMonitor.Hardware;

public sealed class NvidiaSmiReader
{
    private const int ProcessTimeoutMilliseconds = 2_000;
    public NvidiaGpuReading? Read()
    {
        var executable = FindExecutable();
        if (executable is null)
        {
            return null;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.StartInfo.ArgumentList.Add("--query-gpu=utilization.gpu,temperature.gpu,memory.used,memory.total");
            process.StartInfo.ArgumentList.Add("--format=csv,noheader,nounits");

            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ProcessTimeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            if (!Task.WaitAll([outputTask, errorTask], TimeSpan.FromMilliseconds(250))
                || process.ExitCode != 0
                || !outputTask.IsCompletedSuccessfully)
            {
                return null;
            }

            return TryParseOutput(outputTask.Result, out var reading) ? reading : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or AggregateException
                or System.ComponentModel.Win32Exception
                or IOException
                or UnauthorizedAccessException
                or TimeoutException)
        {
            return null;
        }
    }

    public static bool TryParseOutput(string output, out NvidiaGpuReading reading)
    {
        reading = default!;
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = line.Split(',');
            if (columns.Length < 4
                || !TryParseNumber(columns[0], out var gpuLoad)
                || !TryParseNumber(columns[1], out var temperature)
                || !TryParseNumber(columns[2], out var usedMemoryMiB)
                || !TryParseNumber(columns[3], out var totalMemoryMiB)
                || totalMemoryMiB <= 0
                || usedMemoryMiB < 0
                || usedMemoryMiB > totalMemoryMiB)
            {
                continue;
            }

            reading = new NvidiaGpuReading(
                Math.Clamp(gpuLoad, 0d, 100d),
                Math.Max(0d, temperature),
                Math.Clamp(usedMemoryMiB * 100d / totalMemoryMiB, 0d, 100d));
            return true;
        }

        return false;
    }

    private static string? FindExecutable()
    {
        var candidates = new List<string>();
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.AddRange(path.Split(Path.PathSeparator)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => Path.Combine(segment.Trim(), "nvidia-smi.exe")));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"));
        }

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrWhiteSpace(systemDirectory))
        {
            candidates.Add(Path.Combine(systemDirectory, "nvidia-smi.exe"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);
}
