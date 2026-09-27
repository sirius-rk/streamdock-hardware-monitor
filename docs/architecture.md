# Architecture

## Goal

Display useful live CPU, memory, and NVIDIA GPU readings on Stream Dock keys, with short titles and clear unavailable states.

## Supported metrics

| Action | Reading | Source |
| --- | --- | --- |
| CPU Load | Total CPU utilization percentage | Windows `GetSystemTimes`, sampled as a delta |
| Memory Usage | Physical memory utilization percentage | Windows `GlobalMemoryStatusEx` |
| GPU Load | NVIDIA GPU utilization percentage | `nvidia-smi --query-gpu` |
| GPU Temperature | NVIDIA core temperature in Celsius | `nvidia-smi --query-gpu` |
| VRAM Usage | NVIDIA framebuffer memory utilization percentage | `nvidia-smi --query-gpu` |

The initial release does not expose CPU temperature. Windows does not provide a dependable, portable CPU package-temperature API through the standard calls used here. A third-party sensor driver would require a separate security and installation decision.

## Runtime

The plugin is a self-contained .NET 8 Windows x64 executable. It connects to the Stream Dock WebSocket endpoint supplied through process arguments, registers the manifest UUID, and tracks action contexts from `willAppear` / `willDisappear`.

Sampling starts only while at least one action is visible. A single sampler refreshes all active actions every three seconds, so five keys do not cause five independent hardware queries. `GetSystemTimes` needs two samples; CPU load is shown as unavailable until the first interval completes. Memory data is available on the first sample.

For NVIDIA metrics, the sampler locates `nvidia-smi` from `PATH`, the standard NVIDIA NVSMI directory, or the Windows system directory and invokes it without a shell, with a bounded timeout. It queries utilization, temperature, used memory, and total memory in one call and uses the first readable GPU row. Missing utilities, unsupported readings, timeouts, and malformed output produce an unavailable state; errors do not retain stale data.

## Stream Dock events and display

- `willAppear`: add the context and refresh all visible contexts.
- `willDisappear`: remove the context; stop sampling when no contexts remain.
- `deviceDidConnect` / `systemDidWakeUp`: refresh visible contexts.
- `deviceDidDisconnect`: mark visible contexts unavailable.
- Periodic updates use `setTitle` and `setState`.

Each metric has its own action and four manifest states: normal, warning, critical, and unavailable. The title is the compact value only (`42%`, `64°`, or `--`); the action icon identifies the metric. The fixed short title reduces clipping on small Stream Dock keys.

## Initial status thresholds

| Metric | Normal | Warning | Critical |
| --- | --- | --- | --- |
| CPU load | 0–69% | 70–89% | 90–100% |
| Memory usage | 0–79% | 80–89% | 90–100% |
| GPU load | 0–79% | 80–94% | 95–100% |
| GPU temperature | 0–74 °C | 75–84 °C | 85 °C and above |
| VRAM usage | 0–84% | 85–94% | 95–100% |

Unavailable data always uses the neutral state. Thresholds are display hints, not thermal or hardware-control limits.

## Safety and privacy

The plugin only reads Windows counters and NVIDIA query output. It does not change clocks, fan curves, power limits, device settings, or Stream Dock configuration. It does not require administrator rights and does not transmit telemetry. Child process output is parsed locally and errors are not persisted.
