param(
  [string]$InstallDir = $PSScriptRoot,
  [string]$ClaudeExecutable
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
  throw "Visio Scientific Illustrator Test 仅支持 Windows。"
}

$repositoryRoot = (Resolve-Path -LiteralPath $InstallDir).Path
$editionRoot = Join-Path $repositoryRoot "editions\visio-scientific-illustrator-test"
$pluginRoot = Join-Path $editionRoot "plugins\visio-scientific-illustrator-test"
$manifest = Join-Path $pluginRoot ".claude-plugin\plugin.json"
$bridge = Join-Path $pluginRoot "bin\win-x64\VisioTestBridge.exe"
foreach ($requiredPath in @(
    (Join-Path $editionRoot ".claude-plugin\marketplace.json"),
    $manifest,
    $bridge
  )) {
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

if ($ClaudeExecutable) {
  if (-not [IO.Path]::IsPathRooted($ClaudeExecutable) -or
      -not (Test-Path -LiteralPath $ClaudeExecutable -PathType Leaf)) {
    throw "ClaudeExecutable 必须指向存在的绝对文件路径。"
  }
  $claude = (Resolve-Path -LiteralPath $ClaudeExecutable).Path
}
else {
  $claudeCommand = Get-Command claude -ErrorAction SilentlyContinue
  if (-not $claudeCommand) {
    throw "未找到 Claude Code CLI。"
  }
  $claude = $claudeCommand.Source
}

$claudeVersionOutput = (& $claude --version).Trim()
$versionMatch = [regex]::Match($claudeVersionOutput, "^(\d+\.\d+\.\d+)")
if (-not $versionMatch.Success -or
    [version]$versionMatch.Groups[1].Value -lt [version]"2.1.218") {
  throw "需要 Claude Code 2.1.218 或更高版本，当前为 $claudeVersionOutput。"
}

& node (Join-Path $editionRoot "scripts\verify-stable-hashes.mjs")
if ($LASTEXITCODE -ne 0) {
  throw "Stable 保护校验失败，停止安装 Test Edition。"
}
& $claude plugin validate --strict $editionRoot
if ($LASTEXITCODE -ne 0) {
  throw "Claude Test Marketplace 校验失败。"
}
& $claude plugin validate --strict $pluginRoot
if ($LASTEXITCODE -ne 0) {
  throw "Claude Test 插件校验失败。"
}

$marketplaceName = "visio-scientific-tools-test"
$pluginId = "visio-scientific-illustrator-test@$marketplaceName"
$marketplaces = & $claude plugin marketplace list --json | ConvertFrom-Json
$existing = $marketplaces | Where-Object { $_.name -eq $marketplaceName }
if ($existing) {
  if ($existing.source -ne "directory" -or
      [IO.Path]::GetFullPath($existing.path) -ne $editionRoot) {
    throw "Claude Test Marketplace 名称已被其他目录占用。"
  }
}
else {
  & $claude plugin marketplace add $editionRoot --scope user
  if ($LASTEXITCODE -ne 0) {
    throw "注册 Claude Test Marketplace 失败。"
  }
}

$installed = & $claude plugin list --json | ConvertFrom-Json
$testPlugin = $installed | Where-Object { $_.id -eq $pluginId }
if ($testPlugin) {
  & $claude plugin update $pluginId --scope user
}
else {
  & $claude plugin install $pluginId --scope user
}
if ($LASTEXITCODE -ne 0) {
  throw "安装或更新 Claude Test Edition 失败。"
}

$installed = & $claude plugin list --json | ConvertFrom-Json
$testPlugin = $installed | Where-Object { $_.id -eq $pluginId }
$stablePlugin = $installed |
  Where-Object { $_.id -eq "visio-scientific-illustrator@visio-scientific-tools" }
$manifestVersion = (Get-Content -LiteralPath $manifest -Raw -Encoding utf8 |
  ConvertFrom-Json).version
if (-not $testPlugin -or [string]$testPlugin.version -ne [string]$manifestVersion -or
    $testPlugin.enabled -ne $true) {
  throw "Claude Code 没有报告正确且已启用的 Test Edition。"
}
$mcpNames = @($testPlugin.mcpServers.PSObject.Properties.Name | Sort-Object)
if (@(Compare-Object @("visio-test-file-utils", "visio-test-live") $mcpNames).Count -ne 0) {
  throw "Claude Test Edition 的 MCP 清单不正确。"
}

Write-Host "Stable plugin: $(if ($stablePlugin) { 'installed' } else { 'not installed' })"
Write-Host "Test plugin: $pluginId $manifestVersion (enabled)"
Write-Host "执行 /reload-plugins 或新建 Claude Code 会话后使用 Test Edition。"
