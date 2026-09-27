# Development

## Requirements

- Windows x64 and Stream Dock `3.10.188.226` or newer for integration testing.
- .NET 8 SDK. This workstation has no global SDK; during development use the SDK already available in `G:\codes\streamdock-steelseries-battery\.tools\dotnet\dotnet.exe` or install a standard .NET 8 SDK.
- NVIDIA drivers and `nvidia-smi` are needed to verify GPU readings. CPU and memory readers can be tested without an NVIDIA GPU.

## Repository layout

- `src/Hardware/`: Windows CPU/memory counters and the NVIDIA query client.
- `src/Plugin/`: Stream Dock WebSocket lifecycle and action-context updates.
- `tests/`: dependency-free deterministic tests for formatting, thresholds, and `nvidia-smi` parsing.
- `packaging/com.ziheng.streamdock.hardware-monitor.sdPlugin/`: manifest and state artwork.
- `scripts/package.ps1`: self-contained release build and staging.

## Build and test

From the repository root in PowerShell:

```powershell
dotnet build .\src\StreamDockHardwareMonitor.csproj
dotnet run --project .\tests\StreamDockHardwareMonitor.Tests.csproj
.\scripts\package.ps1
dotnet run --project .\src\StreamDockHardwareMonitor.csproj -- --probe
```

On this workstation, replace `dotnet` with `& 'G:\codes\streamdock-steelseries-battery\.tools\dotnet\dotnet.exe'` and pass that executable through `-DotnetPath` to the package script. `--probe` takes two samples one second apart and prints current readings without connecting to Stream Dock.

The package script publishes a self-contained `win-x64` executable and stages a complete `.sdPlugin` folder under `staging/`. Staging output is ignored by Git.

## Installation and manual verification

Copy the staged package to:

```text
%APPDATA%\HotSpot\StreamDock\plugins\com.ziheng.streamdock.hardware-monitor.sdPlugin
```

Then restart Stream Dock manually. On the first load, add one action for each metric. Verify that titles remain compact and that a missing GPU source changes GPU actions to `--` / unavailable instead of leaving old readings on screen. The installer does not stop or restart Stream Dock.

## Release checklist

- Run parser/threshold/unit smoke tests and build a Release package.
- Validate every manifest UUID, image path, and `CodePathWin` target.
- Verify CPU and memory readings on Windows; verify NVIDIA metrics with a live `nvidia-smi` query.
- Keep README and architecture behavior aligned with the implementation.
- Stage English documentation and source files explicitly; Chinese `*.zh.md` documents are maintained locally but must not be committed.
