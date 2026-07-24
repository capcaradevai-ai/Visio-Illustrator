param(
  [string]$InstallDir = $PSScriptRoot,
  [string]$ClaudeExecutable
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core") {
  throw "Visio Scientific Illustrator supports Windows only."
}

$resolvedInstallDir = (Resolve-Path -LiteralPath $InstallDir).Path
$marketplace = Join-Path $resolvedInstallDir ".claude-plugin\marketplace.json"
$pluginRoot = Join-Path $resolvedInstallDir "plugins\visio-scientific-illustrator"
$manifest = Join-Path $pluginRoot ".claude-plugin\plugin.json"
$bridge = Join-Path $pluginRoot "bin\win-x64\VisioBridge.exe"

foreach ($requiredPath in @($marketplace, $manifest, $bridge)) {
  if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
    throw "Required installation file is missing: $requiredPath"
  }
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  throw "Node.js 22 or newer is required."
}
$nodeVersion = (& node --version).Trim()
$nodeMajor = [int]($nodeVersion.TrimStart("v").Split(".")[0])
if ($nodeMajor -lt 22) {
  throw "Node.js 22 or newer is required. Found: $nodeVersion"
}

if ($ClaudeExecutable) {
  if (-not [IO.Path]::IsPathRooted($ClaudeExecutable)) {
    throw "ClaudeExecutable must be an absolute path."
  }
  if (-not (Test-Path -LiteralPath $ClaudeExecutable -PathType Leaf)) {
    throw "Claude Code CLI was not found: $ClaudeExecutable"
  }
  $claude = (Resolve-Path -LiteralPath $ClaudeExecutable).Path
}
else {
  $claudeCommand = Get-Command claude -ErrorAction SilentlyContinue
  if (-not $claudeCommand) {
    throw "Claude Code CLI was not found."
  }
  $claude = $claudeCommand.Source
}

$claudeVersionOutput = (& $claude --version).Trim()
if ($LASTEXITCODE -ne 0) {
  throw "Failed to inspect the Claude Code version."
}
$claudeVersionMatch = [regex]::Match($claudeVersionOutput, "^(\d+\.\d+\.\d+)")
if (-not $claudeVersionMatch.Success) {
  throw "Claude Code returned an unrecognized version: $claudeVersionOutput"
}
$claudeVersion = [version]$claudeVersionMatch.Groups[1].Value
$minimumClaudeVersion = [version]"2.1.218"
if ($claudeVersion -lt $minimumClaudeVersion) {
  throw "Claude Code 2.1.218 or newer is required. Found: $claudeVersionOutput"
}

if ($null -eq [type]::GetTypeFromProgID("Visio.Application", $false)) {
  throw "Microsoft Visio desktop COM automation is not registered."
}

& $claude plugin validate --strict $resolvedInstallDir
if ($LASTEXITCODE -ne 0) {
  throw "Claude Code marketplace validation failed."
}
& $claude plugin validate --strict $pluginRoot
if ($LASTEXITCODE -ne 0) {
  throw "Claude Code plugin validation failed."
}

$marketplaceName = "visio-scientific-tools"
$pluginId = "visio-scientific-illustrator@visio-scientific-tools"
$marketplaces = & $claude plugin marketplace list --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
  throw "Failed to inspect Claude Code marketplaces."
}
$existingMarketplace = $marketplaces | Where-Object { $_.name -eq $marketplaceName }
if ($existingMarketplace) {
  if ($existingMarketplace.source -ne "directory" -or
      [IO.Path]::GetFullPath($existingMarketplace.path) -ne $resolvedInstallDir) {
    throw "The Claude marketplace name is already registered from another source."
  }
  & $claude plugin marketplace remove $marketplaceName --scope user
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to remove the previous Claude marketplace registration."
  }
}

& $claude plugin marketplace add $resolvedInstallDir --scope user
if ($LASTEXITCODE -ne 0) {
  throw "Failed to register the Claude marketplace."
}
& $claude plugin install $pluginId --scope user
if ($LASTEXITCODE -ne 0) {
  throw "Failed to install the Claude plugin."
}

$installed = & $claude plugin list --json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
  throw "Failed to inspect installed Claude plugins."
}
$plugin = $installed | Where-Object { $_.id -eq $pluginId }
if (-not $plugin) {
  throw "Claude Code did not report the plugin as installed."
}
$manifestVersion = (Get-Content -LiteralPath $manifest -Raw -Encoding utf8 |
  ConvertFrom-Json).version
if ([string]$plugin.version -ne [string]$manifestVersion) {
  throw "Installed Claude plugin version does not match $manifestVersion."
}
if ($plugin.enabled -ne $true) {
  throw "Claude Code installed the plugin but did not enable it."
}

$expectedMcpServers = @("visio-file-utils", "visio-live")
$installedMcpServers = @(
  $plugin.mcpServers.PSObject.Properties.Name | Sort-Object
)
if (@(Compare-Object $expectedMcpServers $installedMcpServers).Count -ne 0) {
  throw "Installed Claude plugin does not expose exactly the required MCP servers."
}

$installedPluginRoot = [IO.Path]::GetFullPath([string]$plugin.installPath)
$skillsRoot = Join-Path $installedPluginRoot "skills"
if (-not (Test-Path -LiteralPath $skillsRoot -PathType Container)) {
  throw "Installed Claude plugin is missing its skills directory."
}
$skillDirectories = @(
  Get-ChildItem -LiteralPath $skillsRoot -Directory
)
$expectedSkillName = "recreate-scientific-figure-in-visio"
$installedSkillManifest = if ($skillDirectories.Count -eq 1) {
  Join-Path $skillDirectories[0].FullName "SKILL.md"
}
if ($skillDirectories.Count -ne 1 -or
    $skillDirectories[0].Name -ne $expectedSkillName -or
    -not (Test-Path -LiteralPath $installedSkillManifest -PathType Leaf)) {
  throw "Installed Claude plugin does not contain exactly the required Visio Skill."
}

& $claude plugin details $pluginId
if ($LASTEXITCODE -ne 0) {
  throw "Claude Code could not load the plugin component inventory."
}

Write-Host "Installed $pluginId version $manifestVersion for Claude Code."
Write-Host "Run /reload-plugins or start a new Claude Code session before first use."
