# Stream Dock Hardware Monitor

A Windows Stream Dock plugin for compact, read-only hardware telemetry. Version 0.2.0 exposes six short-title actions: CPU load, memory usage, NVIDIA GPU load, GPU core temperature, GPU memory temperature, and VRAM usage.

## Status

The initial implementation, nine deterministic tests, a live local hardware probe, and a self-contained Windows x64 package are complete. Loading the updated plugin in the Stream Dock UI requires a manual application restart.

## Data sources and scope

- CPU load: Windows `GetSystemTimes` counters.
- System memory: Windows `GlobalMemoryStatusEx`.
- NVIDIA GPU load, core temperature, and framebuffer memory: the installed `nvidia-smi` utility.
- GPU memory temperature: `nvidia-smi` when exposed, with a read-only NVIDIA NVAPI thermal-sensor fallback from the installed graphics driver.
- No kernel sensor driver, administrator privilege, telemetry upload, or hardware-control command is required.
- CPU temperature is intentionally not included: this Windows host does not expose a usable standard ACPI thermal-zone reading, and bundling a kernel sensor driver would add unnecessary privilege and security risk.

Only NVIDIA GPUs are supported by the initial GPU telemetry backend. On systems without a compatible NVIDIA driver/utility, GPU actions display an unavailable state rather than stale values.

On the development workstation, both sources report no memory-target thermal sensor (NVAPI exposes only the GPU-core sensor), so the VRAM-temperature action correctly displays `--`. A usable reading requires a graphics card/driver that exposes a separate memory sensor.

## Display design

Percentage metrics use a live circular progress ring; temperature metrics use a fill-level thermometer. The artwork labels the metric, and short titles include explicit units such as `42%` or `64°C`. Status colors indicate normal, warning, critical, or unavailable readings. If the driver does not expose a memory-temperature sensor, that action shows `--` instead of a guessed value.

See [architecture](docs/architecture.md) for sampling, thresholds, lifecycle, and safety boundaries, and [development](docs/development.md) for build and installation instructions.

## References

- [Stream Dock Plugin SDK](https://sdk.key123.vip/guide/overview.html)
- [NVIDIA System Management Interface](https://docs.nvidia.com/deploy/nvidia-smi/)
- [NVIDIA NVAPI GPU Thermal Control](https://docs.nvidia.com/nvapi/group__gputhermal.html)
- [Windows GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes)
- [Windows GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)
