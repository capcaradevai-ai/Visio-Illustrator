$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\bridge\VisioBridge.Host\VisioBridge.Host.csproj"
$output = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\bin\win-x64"
$manifestPath = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\.codex-plugin\plugin.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$pluginVersion = [string]$manifest.version
if ($pluginVersion -notmatch '^\d+\.\d+\.\d+$') {
  throw "Plugin manifest version must use semantic versioning: $pluginVersion"
}

dotnet publish $project -c Release -r win-x64 --self-contained true -p:Version=$pluginVersion -p:InformationalVersion=$pluginVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) {
  throw "VisioBridge publish failed."
}

$bridge = Join-Path $output "VisioBridge.exe"
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
  throw "Published bridge executable is missing: $bridge"
}

Write-Host "Published Visio Bridge: $bridge"
Write-Host "Plugin and Bridge version: $pluginVersion"
