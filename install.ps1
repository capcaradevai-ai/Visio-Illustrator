param(
  [string]$InstallDir = $PSScriptRoot,
  [string]$CodexExecutable
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
if ($CodexExecutable) {
  if (-not [IO.Path]::IsPathRooted($CodexExecutable)) {
    throw "CodexExecutable must be an absolute path."
  }
  if (-not (Test-Path -LiteralPath $CodexExecutable -PathType Leaf)) {
    throw "Codex CLI was not found: $CodexExecutable"
  }
  $codex = (Resolve-Path -LiteralPath $CodexExecutable).Path
}
else {
  $codexCommand = Get-Command codex -ErrorAction SilentlyContinue
  if (-not $codexCommand) {
    throw "Codex CLI was not found. Install or update the Codex app/CLI first."
  }
  $codex = $codexCommand.Source
}

$visioType = [type]::GetTypeFromProgID("Visio.Application", $false)
if ($null -eq $visioType) {
  throw "Microsoft Visio desktop COM automation is not registered."
}
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
  throw "Bundled VisioBridge.exe is missing. Run npm run build:bridge first."
}

$marketplaceName = "visio-scientific-tools"
$pluginId = "visio-scientific-illustrator@$marketplaceName"
$marketplaces = (& $codex plugin marketplace list --json | ConvertFrom-Json).marketplaces
if ($LASTEXITCODE -ne 0) {
  throw "Failed to inspect configured Codex marketplaces."
}
$existingMarketplace = $marketplaces | Where-Object { $_.name -eq $marketplaceName }
if ($existingMarketplace) {
  if ([IO.Path]::GetFullPath($existingMarketplace.root) -ne $resolvedInstallDir) {
    throw "The Codex marketplace name is already registered from another source."
  }
}
else {
  & $codex plugin marketplace add $resolvedInstallDir
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to register the local Codex marketplace."
  }
}

$manifestVersion = (Get-Content -LiteralPath (Join-Path $pluginRoot ".codex-plugin\plugin.json") -Raw -Encoding utf8 | ConvertFrom-Json).version
$installedPlugins = (& $codex plugin list --json | ConvertFrom-Json).installed
if ($LASTEXITCODE -ne 0) {
  throw "Failed to inspect installed Codex plugins."
}
$installed = $installedPlugins | Where-Object { $_.pluginId -eq $pluginId }
if ([string]$installed.version -ne [string]$manifestVersion) {
  & $codex plugin add $pluginId
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to install the Visio Scientific Illustrator plugin."
  }
}

$installed = (& $codex plugin list --json | ConvertFrom-Json).installed | Where-Object { $_.pluginId -eq $pluginId }
if ($LASTEXITCODE -ne 0 -or -not $installed) {
  throw "Codex did not report the Visio Scientific Illustrator plugin as installed."
}
if ([string]$installed.version -ne [string]$manifestVersion) {
  throw "Installed plugin version does not match the source manifest. Expected $manifestVersion, found $($installed.version)."
}

Write-Host "Installed $pluginId version $manifestVersion"
Write-Host "Restart Codex and start a new task before first use."
