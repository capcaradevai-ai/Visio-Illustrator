# Claude Code 双平台插件打包实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Visio Scientific Illustrator 以单一源码目录发布为同时支持 Codex 和 Claude Code 的 `0.3.0` 插件，并在本机完成双平台安装验证后推送 GitHub。

**Architecture:** `plugins/visio-scientific-illustrator` 继续作为唯一插件根目录。Codex 使用现有 `.agents`、`.codex-plugin` 和 `.mcp.json`；Claude Code 使用新增 `.claude-plugin` 清单和 `.mcp.claude.json`，后者通过 `${CLAUDE_PLUGIN_ROOT}` 定位同一套 MCP 脚本。

**Tech Stack:** Node.js 22、Node Test Runner、PowerShell、Claude Code 2.1.218、Codex CLI、MCP、.NET 8、Microsoft Visio COM。

---

## 文件变更

- Create: `.claude-plugin/marketplace.json` — Claude Code Marketplace。
- Create: `plugins/visio-scientific-illustrator/.claude-plugin/plugin.json` — Claude 插件清单。
- Create: `plugins/visio-scientific-illustrator/.mcp.claude.json` — Claude 缓存安全的 MCP 路径。
- Create: `install-claude.ps1` — Claude Code 安装、更新和验证。
- Create: `tests/node/dual-platform-packaging.test.mjs` — 双清单、版本和路径合同。
- Create: `tests/node/claude-installer.test.mjs` — Claude 安装器静态安全合同。
- Modify: `scripts/validate-repo.mjs` — 同时验证 Codex 与 Claude 打包。
- Modify: `package.json` — 版本升级到 `0.3.0`，加入双平台检查命令。
- Modify: `plugins/visio-scientific-illustrator/.codex-plugin/plugin.json` — Codex 版本升级。
- Modify: `README.md` — 中文双平台安装和使用指南。
- Modify: `README.en.md` — 英文双平台安装和使用指南。
- Modify: `CHANGELOG.md` — 记录 `0.3.0`。

### Task 1: 添加失败的双平台打包合同测试

**Files:**
- Create: `tests/node/dual-platform-packaging.test.mjs`

- [ ] **Step 1: 写入失败测试**

```js
import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const pluginRoot = path.join(root, "plugins", "visio-scientific-illustrator");
const expectedVersion = "0.3.0";

async function readJson(relativePath) {
  return JSON.parse(await fs.readFile(path.join(root, relativePath), "utf8"));
}

test("Codex and Claude manifests publish one plugin at the same version", async () => {
  const packageJson = await readJson("package.json");
  const codexMarketplace = await readJson(".agents/plugins/marketplace.json");
  const claudeMarketplace = await readJson(".claude-plugin/marketplace.json");
  const codexManifest = await readJson(
    "plugins/visio-scientific-illustrator/.codex-plugin/plugin.json",
  );
  const claudeManifest = await readJson(
    "plugins/visio-scientific-illustrator/.claude-plugin/plugin.json",
  );

  assert.equal(packageJson.version, expectedVersion);
  assert.equal(codexMarketplace.name, "visio-scientific-tools");
  assert.equal(claudeMarketplace.name, "visio-scientific-tools");
  assert.equal(codexMarketplace.plugins[0].name, "visio-scientific-illustrator");
  assert.equal(claudeMarketplace.plugins[0].name, "visio-scientific-illustrator");
  assert.equal(codexManifest.name, claudeManifest.name);
  assert.equal(codexManifest.version, expectedVersion);
  assert.equal(claudeManifest.version, expectedVersion);
});

test("Claude MCP configuration resolves scripts through CLAUDE_PLUGIN_ROOT", async () => {
  const mcp = await readJson(
    "plugins/visio-scientific-illustrator/.mcp.claude.json",
  );
  assert.deepEqual(Object.keys(mcp.mcpServers).sort(), [
    "visio-file-utils",
    "visio-live",
  ]);
  for (const definition of Object.values(mcp.mcpServers)) {
    assert.equal(definition.command, "node");
    assert.equal(definition.args.length, 1);
    assert.match(definition.args[0], /^\$\{CLAUDE_PLUGIN_ROOT\}\/scripts\/.+\.mjs$/);
    assert.equal("cwd" in definition, false);
  }
});

test("both platform manifests resolve to the same shared plugin root", async () => {
  const codexMarketplace = await readJson(".agents/plugins/marketplace.json");
  const claudeMarketplace = await readJson(".claude-plugin/marketplace.json");
  const codexSource = codexMarketplace.plugins[0].source.path;
  const claudeSource = claudeMarketplace.plugins[0].source;
  assert.equal(codexSource, "./plugins/visio-scientific-illustrator");
  assert.equal(claudeSource, "./plugins/visio-scientific-illustrator");
  await fs.access(path.join(pluginRoot, "scripts", "live-server.mjs"));
  await fs.access(path.join(pluginRoot, "scripts", "file-server.mjs"));
});
```

- [ ] **Step 2: 运行测试并确认因 Claude 清单缺失而失败**

Run:

```powershell
node --test tests/node/dual-platform-packaging.test.mjs
```

Expected: FAIL，错误包含
`.claude-plugin/marketplace.json` 或 `.mcp.claude.json` 不存在。

- [ ] **Step 3: 提交失败测试**

```powershell
git add -- tests/node/dual-platform-packaging.test.mjs
git commit -m "test: define dual platform plugin packaging contract"
```

### Task 2: 添加 Claude 原生清单并统一版本

**Files:**
- Create: `.claude-plugin/marketplace.json`
- Create: `plugins/visio-scientific-illustrator/.claude-plugin/plugin.json`
- Create: `plugins/visio-scientific-illustrator/.mcp.claude.json`
- Modify: `package.json`
- Modify: `plugins/visio-scientific-illustrator/.codex-plugin/plugin.json`

- [ ] **Step 1: 创建 Claude Marketplace**

```json
{
  "$schema": "https://anthropic.com/claude-code/marketplace.schema.json",
  "name": "visio-scientific-tools",
  "description": "Live, editable scientific illustration in Microsoft Visio.",
  "owner": {
    "name": "Visio Scientific Illustrator Contributors"
  },
  "plugins": [
    {
      "name": "visio-scientific-illustrator",
      "description": "Draw and validate editable scientific figures live in Microsoft Visio through a local COM bridge.",
      "author": {
        "name": "Visio Scientific Illustrator Contributors"
      },
      "category": "productivity",
      "source": "./plugins/visio-scientific-illustrator",
      "homepage": "https://github.com/TZ-mx/Visio-Illustrator"
    }
  ]
}
```

- [ ] **Step 2: 创建 Claude 插件清单**

```json
{
  "name": "visio-scientific-illustrator",
  "version": "0.3.0",
  "description": "Draw editable scientific figures live inside Microsoft Visio through a local COM bridge.",
  "author": {
    "name": "Visio Scientific Illustrator Contributors"
  },
  "homepage": "https://github.com/TZ-mx/Visio-Illustrator",
  "repository": "https://github.com/TZ-mx/Visio-Illustrator",
  "license": "MIT",
  "keywords": [
    "visio",
    "scientific-figure",
    "diagram",
    "mcp"
  ],
  "mcpServers": "./.mcp.claude.json"
}
```

- [ ] **Step 3: 创建 Claude MCP 配置**

```json
{
  "mcpServers": {
    "visio-live": {
      "command": "node",
      "args": [
        "${CLAUDE_PLUGIN_ROOT}/scripts/live-server.mjs"
      ]
    },
    "visio-file-utils": {
      "command": "node",
      "args": [
        "${CLAUDE_PLUGIN_ROOT}/scripts/file-server.mjs"
      ]
    }
  }
}
```

- [ ] **Step 4: 将根包和 Codex 清单版本改为 0.3.0**

在 `package.json` 和
`plugins/visio-scientific-illustrator/.codex-plugin/plugin.json`
中把 `"version": "0.2.0"` 精确改为 `"version": "0.3.0"`。

- [ ] **Step 5: 运行合同测试**

Run:

```powershell
node --test tests/node/dual-platform-packaging.test.mjs
```

Expected: 3 tests PASS。

- [ ] **Step 6: 使用 Claude 官方验证器检查清单**

Run:

```powershell
claude plugin validate --strict .
claude plugin validate --strict .\plugins\visio-scientific-illustrator
```

Expected: 两条命令退出码均为 0，无 warning。

- [ ] **Step 7: 提交清单**

```powershell
git add -- .claude-plugin package.json plugins/visio-scientific-illustrator/.claude-plugin plugins/visio-scientific-illustrator/.mcp.claude.json plugins/visio-scientific-illustrator/.codex-plugin/plugin.json
git commit -m "feat: add native Claude Code plugin packaging"
```

### Task 3: 扩展仓库验证器

**Files:**
- Modify: `scripts/validate-repo.mjs`

- [ ] **Step 1: 在验证器中读取 Claude 清单**

在现有路径定义后加入：

```js
const claudeMarketplacePath = path.join(root, ".claude-plugin", "marketplace.json");
const claudeMarketplace = JSON.parse(
  await fs.readFile(claudeMarketplacePath, "utf8"),
);
const claudeManifestPath = path.join(
  pluginRoot,
  ".claude-plugin",
  "plugin.json",
);
const claudeManifest = JSON.parse(await fs.readFile(claudeManifestPath, "utf8"));
const claudeMcpPath = path.join(pluginRoot, ".mcp.claude.json");
const claudeMcp = JSON.parse(await fs.readFile(claudeMcpPath, "utf8"));
```

- [ ] **Step 2: 添加平台一致性和路径校验**

```js
if (claudeMarketplace.name !== marketplace.name) {
  throw new Error("Codex and Claude marketplace names differ.");
}
if (claudeMarketplace.plugins?.length !== 1) {
  throw new Error("Claude marketplace must expose exactly one plugin.");
}
if (claudeMarketplace.plugins[0].name !== entry.name) {
  throw new Error("Codex and Claude plugin names differ.");
}
if (claudeMarketplace.plugins[0].source !== entry.source.path) {
  throw new Error("Codex and Claude plugin roots differ.");
}
if (claudeManifest.name !== manifest.name) {
  throw new Error("Codex and Claude manifest names differ.");
}
if (claudeManifest.version !== manifest.version) {
  throw new Error("Codex and Claude manifest versions differ.");
}
if (claudeManifest.mcpServers !== "./.mcp.claude.json") {
  throw new Error("Claude manifest must reference .mcp.claude.json.");
}
for (const name of ["visio-live", "visio-file-utils"]) {
  const definition = claudeMcp.mcpServers?.[name];
  if (!definition || definition.command !== "node") {
    throw new Error(`Claude MCP server is missing: ${name}`);
  }
  if (definition.cwd !== undefined) {
    throw new Error(`Claude MCP server must not declare cwd: ${name}`);
  }
  if (!definition.args?.[0]?.startsWith("${CLAUDE_PLUGIN_ROOT}/scripts/")) {
    throw new Error(`Claude MCP server path is not cache-safe: ${name}`);
  }
}
```

- [ ] **Step 3: 将 Claude 文件加入可移植性扫描**

把以下路径加入 `files` 数组：

```js
claudeMarketplacePath,
claudeManifestPath,
claudeMcpPath,
```

- [ ] **Step 4: 运行仓库检查和全部 Node 测试**

Run:

```powershell
npm run check
npm run test:node
```

Expected: Repository validation、MCP smoke 和全部 Node tests PASS。

- [ ] **Step 5: 提交验证器**

```powershell
git add -- scripts/validate-repo.mjs
git commit -m "test: validate Codex and Claude package parity"
```

### Task 4: 测试并实现 Claude 安装器

**Files:**
- Create: `tests/node/claude-installer.test.mjs`
- Create: `install-claude.ps1`

- [ ] **Step 1: 写入安装器合同测试**

```js
import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

test("Claude installer validates prerequisites and installs only Claude plugin state", async () => {
  const script = await fs.readFile(path.join(root, "install-claude.ps1"), "utf8");
  assert.match(script, /Get-Command claude/);
  assert.match(script, /Get-Command node/);
  assert.match(script, /Visio\.Application/);
  assert.match(script, /claude plugin validate/);
  assert.match(script, /plugin marketplace add/);
  assert.match(script, /plugin install/);
  assert.match(script, /plugin list --json/);
  assert.match(script, /plugin details/);
  assert.match(script, /--scope user/);
  assert.match(
    script,
    /visio-scientific-illustrator@visio-scientific-tools/,
  );
  assert.doesNotMatch(script, /codex plugin/);
});
```

- [ ] **Step 2: 运行测试并确认缺少安装器**

Run:

```powershell
node --test tests/node/claude-installer.test.mjs
```

Expected: FAIL，错误为 `install-claude.ps1` 不存在。

- [ ] **Step 3: 创建安装器**

`install-claude.ps1` 使用以下完整流程：

```powershell
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
$nodeMajor = [int]((& node --version).TrimStart("v").Split(".")[0])
if ($nodeMajor -lt 22) {
  throw "Node.js 22 or newer is required. Found: $(& node --version)"
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
$pluginId = "visio-scientific-illustrator@$marketplaceName"
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

& $claude plugin details $pluginId
if ($LASTEXITCODE -ne 0) {
  throw "Claude Code could not load the plugin component inventory."
}

Write-Host "Installed $pluginId version $manifestVersion for Claude Code."
Write-Host "Run /reload-plugins or start a new Claude Code session before first use."
```

- [ ] **Step 4: 运行安装器合同测试**

Run:

```powershell
node --test tests/node/claude-installer.test.mjs
```

Expected: PASS。

- [ ] **Step 5: 提交安装器**

```powershell
git add -- install-claude.ps1 tests/node/claude-installer.test.mjs
git commit -m "feat: add audited Claude Code installer"
```

### Task 5: 更新中英文 README 和变更日志

**Files:**
- Modify: `README.md`
- Modify: `README.en.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: 更新中文 README 快速安装**

在安装章节首先给出：

```markdown
### Codex

```powershell
git clone https://github.com/TZ-mx/Visio-Illustrator.git
Set-Location Visio-Illustrator
.\install.ps1
```

重启 Codex 并新建任务。

### Claude Code

```powershell
git clone https://github.com/TZ-mx/Visio-Illustrator.git
Set-Location Visio-Illustrator
.\install-claude.ps1
```

在 Claude Code 中执行 `/reload-plugins`，或新建会话。
```

- [ ] **Step 2: 加入两个平台的调用方式**

中文 README 明确写入：

```markdown
Codex：在插件选择器中选择 **Visio Scientific Illustrator**，然后直接描述参考图、
输出目录、步进间隔和格式要求。

Claude Code：插件 Skill 名称为
`visio-scientific-illustrator:recreate-scientific-figure-in-visio`。
可以让 Claude 自动选择，也可以显式输入
`/visio-scientific-illustrator:recreate-scientific-figure-in-visio`。
```

- [ ] **Step 3: 增加中文完整提示词**

```text
使用 Visio Scientific Illustrator，在插件拥有的可见 Microsoft Visio 页面中
逐步重绘这张参考图。每步间隔 500 ms。文字、形状和连接线必须保持可编辑，
所有语义连接线的两端必须真正粘附。每完成一个逻辑区域就导出预览并检查，
不要使用系统鼠标键盘自动化，也不要先生成 VSDX 再打开。完成后保存 VSDX，
执行结构与保真校验，并导出宽度 2000 px 的 PNG。
```

- [ ] **Step 4: 在英文 README 写入等价安装、调用、更新和卸载说明**

必须包含以下命令：

```powershell
.\install.ps1
.\install-claude.ps1
claude plugin update visio-scientific-illustrator@visio-scientific-tools
claude plugin uninstall visio-scientific-illustrator@visio-scientific-tools --scope user
claude plugin marketplace remove visio-scientific-tools --scope user
```

- [ ] **Step 5: 在 CHANGELOG 顶部增加 0.3.0**

```markdown
## 0.3.0 — 2026-07-24

- Added native Claude Code marketplace and plugin manifests.
- Added a Claude Code MCP configuration with cache-safe plugin-root paths.
- Added an audited Windows installer for Claude Code.
- Kept one shared Visio Skill, MCP implementation, COM bridge, and asset set for both platforms.
- Added bilingual Codex and Claude Code installation, usage, update, uninstall, and troubleshooting guides.
- Added dual-platform packaging and installer contract tests.
```

- [ ] **Step 6: 检查文档命令和版本**

Run:

```powershell
Select-String -Path README.md,README.en.md,CHANGELOG.md -Pattern "0.2.0|install-claude|claude plugin"
```

Expected: 安装与使用章节包含 Claude 命令；发布版本不再将 `0.2.0`
描述为当前版本。

- [ ] **Step 7: 提交文档**

```powershell
git add -- README.md README.en.md CHANGELOG.md
git commit -m "docs: document Codex and Claude Code installation"
```

### Task 6: 完成全量自动化验证

**Files:**
- Modify: `package.json`

- [ ] **Step 1: 加入 Claude 静态验证命令**

在 `scripts` 中加入：

```json
"validate:claude": "claude plugin validate --strict . && claude plugin validate --strict plugins/visio-scientific-illustrator"
```

把 `test` 改为：

```json
"test": "npm run check && npm run test:node && npm run test:bridge && npm run validate:claude"
```

- [ ] **Step 2: 运行全量测试**

Run:

```powershell
npm test
```

Expected:

- repository validation PASS；
- 两个 MCP server smoke tests PASS；
- 全部 Node tests PASS；
- 非 Visio 集成 .NET tests PASS；
- 两个 Claude strict validation PASS。

- [ ] **Step 3: 检查 Git 差异**

Run:

```powershell
git diff --check
git status --short
```

Expected: 无空白错误；只有本次发布文件和未跟踪的 `.neuralmemory/`。

- [ ] **Step 4: 提交测试命令**

```powershell
git add -- package.json
git commit -m "test: verify Claude plugin packaging in full suite"
```

### Task 7: 本机安装并验证 Claude 与 Codex

**Files:**
- No repository file changes.

- [ ] **Step 1: 安装 Claude 插件**

Run:

```powershell
.\install-claude.ps1
```

Expected: 输出
`Installed visio-scientific-illustrator@visio-scientific-tools version 0.3.0 for Claude Code.`

- [ ] **Step 2: 核验 Claude 插件清单**

Run:

```powershell
claude plugin list --json
claude plugin details visio-scientific-illustrator@visio-scientific-tools
```

Expected:

- 状态为 installed/enabled；
- 版本为 `0.3.0`；
- 一个 Visio Skill；
- MCP servers 包含 `visio-live` 和 `visio-file-utils`。

- [ ] **Step 3: 重新安装并核验 Codex 插件**

Run:

```powershell
.\install.ps1
codex plugin list --json
```

Expected: `visio-scientific-illustrator@visio-scientific-tools`
installed/enabled，版本 `0.3.0`。

- [ ] **Step 4: 运行安装后的 MCP 冒烟测试**

Run:

```powershell
npm run smoke
```

Expected: live server 和 file server 工具清单全部通过。

### Task 8: 发布 GitHub 0.3.0

**Files:**
- No additional source changes.

- [ ] **Step 1: 最终检查提交和工作区**

Run:

```powershell
git log --oneline -8
git status --short --branch
git diff origin/main...HEAD --check
```

Expected: 当前分支只领先双平台发布提交；`.neuralmemory/` 保持未跟踪。

- [ ] **Step 2: 创建发布提交（仅当此前验证产生必要修正）**

```powershell
git add -- .claude-plugin install-claude.ps1 package.json README.md README.en.md CHANGELOG.md scripts/validate-repo.mjs tests/node/dual-platform-packaging.test.mjs tests/node/claude-installer.test.mjs plugins/visio-scientific-illustrator/.claude-plugin plugins/visio-scientific-illustrator/.mcp.claude.json plugins/visio-scientific-illustrator/.codex-plugin/plugin.json
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
  git commit -m "release: publish dual-platform Visio illustrator 0.3.0"
}
```

- [ ] **Step 3: 创建带注释标签**

Run:

```powershell
git tag -a v0.3.0 -m "Visio Scientific Illustrator 0.3.0"
```

Expected: `git tag --list v0.3.0` 输出 `v0.3.0`。

- [ ] **Step 4: 推送当前提交到 origin/main**

Run:

```powershell
git push origin HEAD:main
git push origin v0.3.0
```

Expected: 分支和标签均成功推送到
`https://github.com/TZ-mx/Visio-Illustrator`。

- [ ] **Step 5: 验证远程状态**

Run:

```powershell
git ls-remote origin refs/heads/main refs/tags/v0.3.0
```

Expected: 同时返回 `refs/heads/main` 和 `refs/tags/v0.3.0`。
