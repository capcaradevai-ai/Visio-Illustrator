$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\bridge\VisioBridge.Host\VisioBridge.Host.csproj"
$output = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\bin\win-x64"

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) {
  throw "VisioBridge publish failed."
}

$bridge = Join-Path $output "VisioBridge.exe"
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
  throw "Published bridge executable is missing: $bridge"
}

Write-Host "Published Visio Bridge: $bridge"
