$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator-test\bridge\VisioTestBridge.Host\VisioTestBridge.Host.csproj"
$output = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator-test\bin\win-x64"
$manifestPath = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator-test\.codex-plugin\plugin.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$pluginVersion = [string]$manifest.version
if ($pluginVersion -notmatch '^\d+\.\d+\.\d+-test\.\d+$') {
  throw "Test plugin manifest version must use prerelease semantic versioning: $pluginVersion"
}

dotnet publish $project -c Release -r win-x64 --self-contained true -p:Version=$pluginVersion -p:InformationalVersion=$pluginVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) {
  throw "VisioTestBridge publish failed."
}

$bridge = Join-Path $output "VisioTestBridge.exe"
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
  throw "Published Test Bridge executable is missing: $bridge"
}
if (Test-Path -LiteralPath (Join-Path $output "VisioBridge.exe") -PathType Leaf) {
  throw "Stable Bridge executable must not exist in Test output."
}

Write-Host "Published Visio Test Bridge: $bridge"
Write-Host "Plugin and Bridge version: $pluginVersion"
