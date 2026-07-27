param(
  [string]$InstallDir = $PSScriptRoot,
  [string]$CodexExecutable
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
  throw "Visio Scientific Illustrator Test 仅支持 Windows。"
}

$repositoryRoot = (Resolve-Path -LiteralPath $InstallDir).Path
$editionRoot = Join-Path $repositoryRoot "editions\visio-scientific-illustrator-test"
$pluginRoot = Join-Path $editionRoot "plugins\visio-scientific-illustrator-test"
$marketplace = Join-Path $editionRoot ".agents\plugins\marketplace.json"
$manifest = Join-Path $pluginRoot ".codex-plugin\plugin.json"
$bridge = Join-Path $pluginRoot "bin\win-x64\VisioTestBridge.exe"

foreach ($requiredPath in @($marketplace, $manifest, $bridge)) {
  if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
    throw "缺少 Test Edition 安装文件：$requiredPath"
  }
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  throw "需要 Node.js 22 或更高版本。"
}
$nodeVersion = (& node --version).Trim()
if ([int]($nodeVersion.TrimStart("v").Split(".")[0]) -lt 22) {
  throw "需要 Node.js 22 或更高版本，当前为 $nodeVersion。"
}
if ($null -eq [type]::GetTypeFromProgID("Visio.Application", $false)) {
  throw "没有检测到 Microsoft Visio 桌面版 COM 注册。"
}

& node (Join-Path $editionRoot "scripts\verify-stable-hashes.mjs")
if ($LASTEXITCODE -ne 0) {
  throw "Stable 保护校验失败，停止安装 Test Edition。"
}
& node (Join-Path $editionRoot "scripts\validate-repo.mjs")
if ($LASTEXITCODE -ne 0) {
  throw "Test Edition 仓库校验失败。"
}

if ($CodexExecutable) {
  if (-not [IO.Path]::IsPathRooted($CodexExecutable) -or
      -not (Test-Path -LiteralPath $CodexExecutable -PathType Leaf)) {
    throw "CodexExecutable 必须指向存在的绝对文件路径。"
  }
  $codex = (Resolve-Path -LiteralPath $CodexExecutable).Path
}
else {
  $codexCommand = Get-Command codex -ErrorAction SilentlyContinue
  if (-not $codexCommand) {
    throw "未找到 Codex CLI。"
  }
  $codex = $codexCommand.Source
}

$marketplaceName = "visio-scientific-tools-test"
$pluginId = "visio-scientific-illustrator-test@$marketplaceName"
$marketplaces = (& $codex plugin marketplace list --json | ConvertFrom-Json).marketplaces
if ($LASTEXITCODE -ne 0) {
  throw "无法读取 Codex Marketplace。"
}
$existingMarketplace = $marketplaces | Where-Object { $_.name -eq $marketplaceName }
if ($existingMarketplace) {
  if ([IO.Path]::GetFullPath($existingMarketplace.root) -ne $editionRoot) {
    throw "Test Marketplace 名称已被其他目录占用。"
  }
}
else {
  & $codex plugin marketplace add $editionRoot
  if ($LASTEXITCODE -ne 0) {
    throw "注册 Test Marketplace 失败。"
  }
}

$manifestVersion = (Get-Content -LiteralPath $manifest -Raw -Encoding utf8 |
  ConvertFrom-Json).version
$installedPlugins = (& $codex plugin list --json | ConvertFrom-Json).installed
if ($LASTEXITCODE -ne 0) {
  throw "无法读取 Codex 插件列表。"
}
$installed = $installedPlugins | Where-Object { $_.pluginId -eq $pluginId }
if (-not $installed) {
  & $codex plugin add $pluginId
}
elseif ([string]$installed.version -ne [string]$manifestVersion) {
  & $codex plugin update $pluginId
}
if ($LASTEXITCODE -ne 0) {
  throw "安装或更新 Test Edition 失败。"
}

$allInstalled = (& $codex plugin list --json | ConvertFrom-Json).installed
$testPlugin = $allInstalled | Where-Object { $_.pluginId -eq $pluginId }
$stablePlugin = $allInstalled |
  Where-Object { $_.pluginId -eq "visio-scientific-illustrator@visio-scientific-tools" }
if (-not $testPlugin -or [string]$testPlugin.version -ne [string]$manifestVersion) {
  throw "Codex 没有报告正确版本的 Test Edition。"
}

Write-Host "Stable plugin: $(
  if ($stablePlugin) { 'visio-scientific-illustrator@visio-scientific-tools (enabled)' }
  else { 'not installed' }
)"
Write-Host "Test plugin: $pluginId $manifestVersion (enabled)"
Write-Host "请重启 Codex 或新建任务后使用 Test Edition。"
