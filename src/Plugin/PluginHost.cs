using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamDockHardwareMonitor.Hardware;

namespace StreamDockHardwareMonitor.Plugin;

public static class PluginHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = PluginOptions.Parse(args);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            await using var plugin = new StreamDockPlugin(options, new HardwareMetricsReader());
            await plugin.RunAsync(cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}

internal sealed record PluginOptions(int Port, string PluginUuid, string RegisterEvent)
{
    public static PluginOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith("-", StringComparison.Ordinal) && index + 1 < args.Length)
            {
                values[args[index]] = args[++index];
            }
        }

        if (!values.TryGetValue("-port", out var portText)
            || !int.TryParse(portText, out var port)
            || port is <= 0 or > 65535)
        {
            throw new ArgumentException("Stream Dock did not provide a valid -port argument.");
        }

        return new PluginOptions(
            port,
            Required(values, "-pluginUUID"),
            Required(values, "-registerEvent"));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Stream Dock did not provide {name}.");
}

internal sealed class StreamDockPlugin : IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private readonly PluginOptions _options;
    private readonly HardwareMetricsReader _reader;
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<string, MetricKind> _contexts = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _pollTask;

    public StreamDockPlugin(PluginOptions options, HardwareMetricsReader reader)
    {
        _options = options;
        _reader = reader;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token);
        var token = linkedCancellation.Token;

        await _socket.ConnectAsync(new Uri($"ws://127.0.0.1:{_options.Port}"), token);
        await SendAsync(new { @event = _options.RegisterEvent, uuid = _options.PluginUuid }, token);
        _pollTask = PollLoopAsync(token);

        try
        {
            await ReceiveLoopAsync(token);
        }
        finally
        {
            _lifetime.Cancel();
            if (_pollTask is not null)
            {
                try
                {
                    await _pollTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when Stream Dock closes the plugin connection.
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _socket.Dispose();
        _refreshGate.Dispose();
        _sendGate.Dispose();
        _lifetime.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();

        while (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            await HandleMessageAsync(
                Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length),
                cancellationToken);
        }
    }

    private async Task HandleMessageAsync(string json, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var eventName = root.TryGetProperty("event", out var eventValue)
            ? eventValue.GetString()
            : null;
        var actionUuid = root.TryGetProperty("action", out var actionValue)
            ? actionValue.GetString()
            : null;

        switch (eventName)
        {
            case "willAppear":
                if (ActionDefinition.TryGet(actionUuid, out var action)
                    && TryGetContext(root, out var appearingContext))
                {
                    _contexts[appearingContext] = action.Metric;
                    await RefreshVisibleAsync(cancellationToken);
                }
                break;

            case "willDisappear":
                if (TryGetContext(root, out var disappearingContext))
                {
                    _contexts.TryRemove(disappearingContext, out _);
                }
                break;

            case "deviceDidConnect":
            case "systemDidWakeUp":
                await RefreshVisibleAsync(cancellationToken);
                break;

            case "deviceDidDisconnect":
                await SetUnavailableForAllAsync(cancellationToken);
                break;
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!_contexts.IsEmpty)
            {
                await RefreshVisibleAsync(cancellationToken);
            }
        }
    }

    private async Task RefreshVisibleAsync(CancellationToken cancellationToken)
    {
        if (_contexts.IsEmpty || !await _refreshGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var snapshot = await Task.Run(_reader.Capture, cancellationToken);
            foreach (var (context, metric) in _contexts.ToArray())
            {
                var value = snapshot.Get(metric);
                await SetMetricAsync(context, metric, value, cancellationToken);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task SetUnavailableForAllAsync(CancellationToken cancellationToken)
    {
        foreach (var (context, metric) in _contexts.ToArray())
        {
            await SetMetricAsync(context, metric, null, cancellationToken);
        }
    }

    private async Task SetMetricAsync(
        string context,
        MetricKind metric,
        double? value,
        CancellationToken cancellationToken)
    {
        var state = (int)MetricPresentation.GetState(metric, value);
        await SendAsync(
            new { @event = "setState", context, payload = new { state } },
            cancellationToken);
        await SendAsync(
            new
            {
                @event = "setTitle",
                context,
                payload = new { title = MetricPresentation.FormatTitle(metric, value), target = 0 }
            },
            cancellationToken);
    }

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static bool TryGetContext(JsonElement root, out string context)
    {
        if (root.TryGetProperty("context", out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            context = value.GetString()!;
            return true;
        }

        context = string.Empty;
        return false;
    }
}
