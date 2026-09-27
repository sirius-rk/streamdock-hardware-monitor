param(
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\StreamDockHardwareMonitor.csproj"
$sourcePluginPath = Join-Path $repoRoot "packaging\com.ziheng.streamdock.hardware-monitor.sdPlugin"
$outputPath = Join-Path $repoRoot "staging\com.ziheng.streamdock.hardware-monitor.sdPlugin"

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$publishArguments = @(
    "publish",
    $projectPath,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-o", $outputPath
)

& $DotnetPath @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$manifest = Get-Content -Raw (Join-Path $sourcePluginPath "manifest.json") | ConvertFrom-Json
$publishedExe = Join-Path $outputPath $manifest.CodePathWin
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published executable not found: $publishedExe. Ensure the project assembly name matches manifest CodePathWin."
}

Copy-Item -LiteralPath (Join-Path $sourcePluginPath "manifest.json") -Destination $outputPath
New-Item -ItemType Directory -Path (Join-Path $outputPath "images") -Force | Out-Null
Copy-Item -Path (Join-Path $sourcePluginPath "images\*") -Destination (Join-Path $outputPath "images") -Recurse -Force

Write-Host "Packaged plugin: $outputPath"
