# Stream Dock Hardware Monitor

A Windows Stream Dock plugin for compact, read-only hardware telemetry. The planned first release exposes five short-title actions: CPU load, memory usage, NVIDIA GPU load, NVIDIA GPU temperature, and NVIDIA VRAM usage.

## Status

The initial implementation, seven deterministic tests, a live local hardware probe, and a self-contained Windows x64 package are complete. Loading the plugin in the Stream Dock UI still requires a manual application restart.

## Data sources and scope

- CPU load: Windows `GetSystemTimes` counters.
- System memory: Windows `GlobalMemoryStatusEx`.
- NVIDIA GPU load, temperature, and framebuffer memory: the installed `nvidia-smi` utility.
- No kernel sensor driver, administrator privilege, telemetry upload, or hardware-control command is required.
- CPU temperature is intentionally not included: this Windows host does not expose a usable standard ACPI thermal-zone reading, and bundling a kernel sensor driver would add unnecessary privilege and security risk.

Only NVIDIA GPUs are supported by the initial GPU telemetry backend. On systems without a compatible NVIDIA driver/utility, GPU actions display an unavailable state rather than stale values.

## Display design

Each metric has its own action icon, while the title contains only a compact value such as `42%` or `64°`. This keeps labels short on small keys. Status colors indicate normal, warning, critical, or unavailable readings.

See [architecture](docs/architecture.md) for sampling, thresholds, lifecycle, and safety boundaries, and [development](docs/development.md) for build and installation instructions.

## References

- [Stream Dock Plugin SDK](https://sdk.key123.vip/guide/overview.html)
- [NVIDIA System Management Interface](https://docs.nvidia.com/deploy/nvidia-smi/)
- [Windows GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes)
- [Windows GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)
