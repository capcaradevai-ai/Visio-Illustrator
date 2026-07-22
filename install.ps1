param(
  [string]$InstallDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
  throw "Visio Scientific Illustrator supports Windows only."
}

$resolvedInstallDir = (Resolve-Path -LiteralPath $InstallDir).Path
$marketplace = Join-Path $resolvedInstallDir ".agents\plugins\marketplace.json"
$pluginRoot = Join-Path $resolvedInstallDir "plugins\visio-scientific-illustrator"
$bridge = Join-Path $pluginRoot "bin\win-x64\VisioBridge.exe"

if (-not (Test-Path -LiteralPath $marketplace -PathType Leaf)) {
  throw "Codex marketplace metadata is missing: $marketplace"
}
if (-not (Test-Path -LiteralPath $pluginRoot -PathType Container)) {
  throw "Plugin directory is missing: $pluginRoot"
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  throw "Node.js 22 or newer is required."
}
$nodeMajor = [int]((& node --version).TrimStart("v").Split(".")[0])
if ($nodeMajor -lt 22) {
  throw "Node.js 22 or newer is required. Found: $(& node --version)"
}
if (-not (Get-Command codex -ErrorAction SilentlyContinue)) {
  throw "Codex CLI was not found. Install or update the Codex app/CLI first."
}

$visioType = [type]::GetTypeFromProgID("Visio.Application", $false)
if ($null -eq $visioType) {
  throw "Microsoft Visio desktop COM automation is not registered."
}
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
  throw "Bundled VisioBridge.exe is missing. Run npm run build:bridge first."
}

codex plugin marketplace add $resolvedInstallDir
if ($LASTEXITCODE -ne 0) {
  throw "Failed to register the local Codex marketplace."
}

codex plugin add "visio-scientific-illustrator@visio-scientific-tools"
if ($LASTEXITCODE -ne 0) {
  throw "Failed to install the Visio Scientific Illustrator plugin."
}

Write-Host "Installed visio-scientific-illustrator@visio-scientific-tools"
Write-Host "Restart Codex and start a new task before first use."
