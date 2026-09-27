using System.Globalization;
using System.Text;

namespace StreamDockHardwareMonitor.Hardware;

public static class MetricIconRenderer
{
    private const double GaugeRadius = 25d;
    private const double GaugeCircumference = 2d * Math.PI * GaugeRadius;

    public static string RenderDataUri(MetricKind metric, double? value)
    {
        var fraction = MetricPresentation.GetGaugeFraction(metric, value);
        var state = MetricPresentation.GetState(metric, value);
        var color = state switch
        {
            MetricDisplayState.Normal => "#42d392",
            MetricDisplayState.Warning => "#ffbf47",
            MetricDisplayState.Critical => "#ff5968",
            _ => "#788695"
        };
        var fill = Format(GaugeCircumference * (1d - fraction));
        var center = MetricPresentation.IsTemperature(metric)
            ? RenderThermometer(metric, fraction, color)
            : RenderPercentGauge(metric, fill, color);

        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"72\" height=\"72\" viewBox=\"0 0 72 72\">"
            + $"<rect x=\"3\" y=\"3\" width=\"66\" height=\"66\" rx=\"18\" fill=\"#202a34\" stroke=\"{color}\" stroke-width=\"3\"/>"
            + center
            + "</svg>";
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    private static string RenderPercentGauge(MetricKind metric, string dashOffset, string color)
    {
        var label = metric switch
        {
            MetricKind.CpuLoad => "CPU",
            MetricKind.MemoryUsage => "RAM",
            MetricKind.GpuLoad => "GPU",
            MetricKind.VramUsage => "VRAM",
            _ => "?"
        };
        var fontSize = label.Length > 3 ? 10 : 12;
        return "<circle cx=\"36\" cy=\"35\" r=\"25\" fill=\"none\" stroke=\"#455462\" stroke-width=\"6\"/>"
            + $"<circle cx=\"36\" cy=\"35\" r=\"25\" fill=\"none\" stroke=\"{color}\" stroke-width=\"6\" stroke-linecap=\"round\" stroke-dasharray=\"{Format(GaugeCircumference)}\" stroke-dashoffset=\"{dashOffset}\" transform=\"rotate(-90 36 35)\"/>"
            + $"<text x=\"36\" y=\"39\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"{fontSize}\" font-weight=\"700\" fill=\"#f3f7fa\">{label}</text>";
    }

    private static string RenderThermometer(MetricKind metric, double fraction, string color)
    {
        var label = metric == MetricKind.VramTemperature ? "VRAM" : "CORE";
        var mercuryHeight = 22d * fraction;
        var mercuryTop = 48d - mercuryHeight;
        return $"<text x=\"36\" y=\"17\" text-anchor=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"9\" font-weight=\"700\" letter-spacing=\"1\" fill=\"#dce7ef\">{label}</text>"
            + "<rect x=\"31\" y=\"22\" width=\"10\" height=\"34\" rx=\"5\" fill=\"#354451\" stroke=\"#dce7ef\" stroke-width=\"2\"/>"
            + $"<rect x=\"34\" y=\"{Format(mercuryTop)}\" width=\"4\" height=\"{Format(mercuryHeight)}\" rx=\"2\" fill=\"{color}\"/>"
            + "<circle cx=\"36\" cy=\"55\" r=\"8\" fill=\"#354451\" stroke=\"#dce7ef\" stroke-width=\"2\"/>"
            + $"<circle cx=\"36\" cy=\"55\" r=\"4.5\" fill=\"{color}\"/>"
            + "<path d=\"M45 29h5m-5 8h5m-5 8h5\" stroke=\"#dce7ef\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>";
    }

    private static string Format(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
