# Test Edition Phase 1：隔离骨架与协议身份实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` and `test-driven-development`. Execute only after explicit user approval.

**Goal:** 从稳定版复制一份一次性源码基线，建立完全独立的 Test Edition 目录、构建、清单、MCP、Bridge 身份和稳定版 hash 保护。

**Architecture:** Test Edition 不在运行时引用稳定版。根目录安装器只作为进入独立 edition 的薄入口；所有源码、测试和构建脚本位于 `editions/visio-scientific-illustrator-test/`。

**Tech Stack:** Node.js 22、Node Test Runner、PowerShell、.NET 8、Codex/Claude 插件清单。

**Path Key:** 下文 `TEST_PLUGIN_ROOT` 严格表示仓库相对路径 `editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test`；`TEST_BRIDGE_ROOT` 严格表示 `TEST_PLUGIN_ROOT/bridge`。

---

## Task 1：建立稳定版受保护文件基线

**Files:**

- Create: `editions/visio-scientific-illustrator-test/scripts/capture-stable-hashes.mjs`
- Create: `editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs`
- Create: `editions/visio-scientific-illustrator-test/stable-protected.sha256.json`
- Create: `editions/visio-scientific-illustrator-test/tests/node/stable-protection.test.mjs`

- [ ] **Step 1：写失败测试**

测试必须枚举 Git 已跟踪文件，不扫描 `.git`、`bin` 临时输出或 `.neuralmemory`。固定受保护入口如下：

```js
const protectedRoots = [
  "plugins/visio-scientific-illustrator",
  ".agents/plugins/marketplace.json",
  ".claude-plugin/marketplace.json",
  "install.ps1",
  "install-claude.ps1",
];
```

断言：

```js
assert.equal(manifest.algorithm, "sha256");
assert.equal(manifest.source_commit, "69df39c641f38574110563ee343f2a4d8fad30a3");
assert.ok(Object.keys(manifest.files).length > 100);
assert.deepEqual(await verifyStableHashes({ root, manifest }), {
  ok: true,
  changed: [],
  missing: [],
  unexpected: [],
});
```

- [ ] **Step 2：运行失败测试**

Run:

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/stable-protection.test.mjs
```

Expected: FAIL，原因是 hash 脚本和 manifest 尚不存在。

- [ ] **Step 3：实现确定性 hash 捕获和校验**

实现要求：

- 路径统一为 `/`；
- 文件按 `String.localeCompare(..., "en")` 排序；
- 内容按原始字节计算 SHA-256；
- manifest 使用 UTF-8、两个空格缩进、末尾换行；
- 校验同时报告 `changed`、`missing`、`unexpected`；
- 校验失败时进程退出码为 `1`；
- 不允许通过重新捕获基线来掩盖稳定版变化。

- [ ] **Step 4：生成一次性基线并通过测试**

Run:

```powershell
node editions/visio-scientific-illustrator-test/scripts/capture-stable-hashes.mjs --source-commit 69df39c641f38574110563ee343f2a4d8fad30a3
node --test editions/visio-scientific-illustrator-test/tests/node/stable-protection.test.mjs
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
```

Expected:

```text
Stable protected files unchanged.
```

- [ ] **Step 5：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/scripts editions/visio-scientific-illustrator-test/tests/node/stable-protection.test.mjs editions/visio-scientific-illustrator-test/stable-protected.sha256.json
git commit -m "test(test-edition): protect stable plugin bytes"
```

## Task 2：复制源码并建立独立包结构

**Files:**

- Create: `editions/visio-scientific-illustrator-test/package.json`
- Create: `editions/visio-scientific-illustrator-test/.agents/plugins/marketplace.json`
- Create: `editions/visio-scientific-illustrator-test/.claude-plugin/marketplace.json`
- Create: `editions/visio-scientific-illustrator-test/scripts/build-bridge.ps1`
- Create: `editions/visio-scientific-illustrator-test/scripts/validate-repo.mjs`
- Create: `editions/visio-scientific-illustrator-test/scripts/smoke-test.mjs`
- Create: `editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/**`
- Create: `editions/visio-scientific-illustrator-test/tests/fixtures/scientific-scene/reference-preview.png`
- Create: `editions/visio-scientific-illustrator-test/tests/fixtures/scientific-scene/reference-scene.json`
- Create: `editions/visio-scientific-illustrator-test/tests/node/bridge-client.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/bridge-stop-runner.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/fake-bridge.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/fidelity-baseline.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/mcp-common.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/server-routing.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/skill-content.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/tool-schemas.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/visual-compare.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/identity-isolation.test.mjs`

- [ ] **Step 1：写身份合同测试**

固定断言：

```js
const expected = {
  packageName: "visio-scientific-tools-test",
  pluginName: "visio-scientific-illustrator-test",
  displayName: "Visio Scientific Illustrator Test",
  version: "0.4.0-test.1",
  liveServer: "visio-test-live",
  fileServer: "visio-test-file-utils",
};
```

测试还必须断言：

- 两个 Marketplace 都只暴露 Test Edition；
- Codex 和 Claude manifest 指向同一个 Test 插件根目录；
- Test Edition 目录不含相对路径跳转到 `plugins/visio-scientific-illustrator`；
- Test 包的运行时文件中不存在稳定版 MCP 名称；
- stable hash 校验仍通过。

- [ ] **Step 2：运行失败测试**

Run:

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/identity-isolation.test.mjs
```

Expected: FAIL，原因是 Test 插件清单和独立源码尚未创建。

- [ ] **Step 3：复制一次稳定版源码**

复制以下内容到 Test 插件根目录，并保留目录层次：

```text
assets/
bridge/
scripts/
skills/
.codex-plugin/
.claude-plugin/
.mcp.json
.mcp.claude.json
```

复制时排除：

```text
bridge/**/bin/**
bridge/**/obj/**
bridge/**/TestResults/**
bin/**
```

随后只在 Test Edition 副本内修改身份。不得建立符号链接、junction、运行时 import 或构建时引用回稳定版。

再从仓库根目录复制以下测试和构建基线到 edition 内，并立即把相对路径改为 Test 插件根目录：

```text
scripts/build-bridge.ps1
scripts/validate-repo.mjs
scripts/smoke-test.mjs
tests/fixtures/scientific-scene/reference-preview.png
tests/fixtures/scientific-scene/reference-scene.json
tests/node/bridge-client.test.mjs
tests/node/bridge-stop-runner.mjs
tests/node/fake-bridge.mjs
tests/node/fidelity-baseline.test.mjs
tests/node/mcp-common.test.mjs
tests/node/server-routing.test.mjs
tests/node/skill-content.test.mjs
tests/node/tool-schemas.test.mjs
tests/node/visual-compare.test.mjs
```

稳定版安装器测试只作为实现参考，不直接复制到 Test 测试集；Test 安装器合同在 Phase 5 单独创建。复制的其余测试必须把工具前缀、Bridge 路径和期望插件身份改成 Test 固定值。

- [ ] **Step 4：写独立清单和调整复制测试**

Test package 的脚本至少包括：

```json
{
  "scripts": {
    "check": "node scripts/validate-repo.mjs && npm run smoke",
    "smoke": "node scripts/smoke-test.mjs",
    "test:node": "node --test tests/node/*.test.mjs",
    "build:bridge": "powershell -ExecutionPolicy Bypass -File scripts/build-bridge.ps1",
    "test:bridge": "dotnet test plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.sln -c Release --filter \"Category!=VisioIntegration\"",
    "test": "npm run check && npm run test:node && npm run test:bridge"
  }
}
```

所有新清单必须为 UTF-8，中文默认提示不得包含 `�` 或乱码字节序列。

- [ ] **Step 5：运行身份测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/identity-isolation.test.mjs
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
```

Expected: PASS。

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add isolated plugin source tree"
```

## Task 3：重命名 Bridge 程序集、命名空间和可执行文件

**Files:**

- Move: `TEST_BRIDGE_ROOT/VisioBridge.sln` → `TEST_BRIDGE_ROOT/VisioTestBridge.sln`
- Move: `TEST_BRIDGE_ROOT/VisioBridge.Contracts/` → `TEST_BRIDGE_ROOT/VisioTestBridge.Contracts/`
- Move: `TEST_BRIDGE_ROOT/VisioBridge.Core/` → `TEST_BRIDGE_ROOT/VisioTestBridge.Core/`
- Move: `TEST_BRIDGE_ROOT/VisioBridge.Host/` → `TEST_BRIDGE_ROOT/VisioTestBridge.Host/`
- Move: `TEST_BRIDGE_ROOT/VisioBridge.Tests/` → `TEST_BRIDGE_ROOT/VisioTestBridge.Tests/`
- Move: `TEST_BRIDGE_ROOT/VisioBridge.IntegrationTests/` → `TEST_BRIDGE_ROOT/VisioTestBridge.IntegrationTests/`
- Move: `TEST_BRIDGE_ROOT/VisioTestBridge.Contracts/VisioBridge.Contracts.csproj` → `TEST_BRIDGE_ROOT/VisioTestBridge.Contracts/VisioTestBridge.Contracts.csproj`
- Move: `TEST_BRIDGE_ROOT/VisioTestBridge.Core/VisioBridge.Core.csproj` → `TEST_BRIDGE_ROOT/VisioTestBridge.Core/VisioTestBridge.Core.csproj`
- Move: `TEST_BRIDGE_ROOT/VisioTestBridge.Host/VisioBridge.Host.csproj` → `TEST_BRIDGE_ROOT/VisioTestBridge.Host/VisioTestBridge.Host.csproj`
- Move: `TEST_BRIDGE_ROOT/VisioTestBridge.Tests/VisioBridge.Tests.csproj` → `TEST_BRIDGE_ROOT/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj`
- Move: `TEST_BRIDGE_ROOT/VisioTestBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj` → `TEST_BRIDGE_ROOT/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj`
- Modify: `editions/visio-scientific-illustrator-test/scripts/build-bridge.ps1`
- Create: `editions/visio-scientific-illustrator-test/tests/node/bridge-identity.test.mjs`

- [ ] **Step 1：写 Bridge 身份测试**

断言：

- solution 名为 `VisioTestBridge.sln`；
-所有 `AssemblyName` 以 `VisioTestBridge` 开头；
- C# 命名空间不含 `namespace VisioBridge`；
-发布输出固定为 `bin/win-x64/VisioTestBridge.exe`；
-不存在 `bin/win-x64/VisioBridge.exe`；
- Bridge ready 消息包含 `edition: "test"` 和 `protocolVersion: "0.4.0-test.1"`。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/bridge-identity.test.mjs
```

Expected: FAIL，列出仍使用 `VisioBridge` 的 Test Edition 文件。

- [ ] **Step 3：机械重命名后逐文件修复**

仅在 Test Edition 内执行重命名，随后更新：

- `.sln` 项目路径和 GUID 引用；
- `.csproj` 的 `AssemblyName`、`RootNamespace`、`ProjectReference`；
- 所有 `namespace` 和 `using`；
- Host 的产品名、互斥体名、日志标签和临时目录前缀；
- `BridgeReady` 增加 `Edition` 与 `ProtocolVersion`；
-构建脚本读取 Test Codex manifest，接受预发布语义版本 `^\d+\.\d+\.\d+-test\.\d+$`。

- [ ] **Step 4：构建并运行非 Visio 测试**

```powershell
dotnet build editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.sln -c Release
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.sln -c Release --filter "Category!=VisioIntegration"
node --test editions/visio-scientific-illustrator-test/tests/node/bridge-identity.test.mjs
```

Expected: PASS，且稳定版 build 输出未被写入。

- [ ] **Step 5：发布 Test Bridge**

```powershell
powershell -ExecutionPolicy Bypass -File editions/visio-scientific-illustrator-test/scripts/build-bridge.ps1
```

Expected:

```text
Published Visio Test Bridge: ...\bin\win-x64\VisioTestBridge.exe
Plugin and Bridge version: 0.4.0-test.1
```

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bin editions/visio-scientific-illustrator-test/scripts/build-bridge.ps1 editions/visio-scientific-illustrator-test/tests/node/bridge-identity.test.mjs
git commit -m "refactor(test-edition): isolate bridge identity"
```

## Task 4：重命名 MCP server、工具前缀和错误工具名

**Files:**

- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/file-server.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/mcp-common.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/bridge-client.mjs`
- Modify: `TEST_PLUGIN_ROOT/.mcp.json`
- Modify: `TEST_PLUGIN_ROOT/.mcp.claude.json`
- Create: `editions/visio-scientific-illustrator-test/tests/node/tool-prefix.test.mjs`
- Modify: `editions/visio-scientific-illustrator-test/tests/node/server-routing.test.mjs`

- [ ] **Step 1：写失败测试**

断言所有公开工具：

```js
assert.ok(liveTools.every((tool) => tool.name.startsWith("visio_test_live_")));
assert.ok(fileTools.every((tool) =>
  tool.name.startsWith("visio_test_") &&
  !tool.name.startsWith("visio_test_live_"),
));
```

还需断言错误对象中的 `tool`、sequence 错误映射和 server instructions 使用 Test 工具名。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/tool-prefix.test.mjs editions/visio-scientific-illustrator-test/tests/node/server-routing.test.mjs
```

Expected: FAIL，原因是复制代码仍暴露 `visio_live_*`。

- [ ] **Step 3：完成协议重命名**

映射规则：

```text
visio-live             -> visio-test-live
visio-file-utils       -> visio-test-file-utils
visio_live_<name>      -> visio_test_live_<name>
visio_<file-name>      -> visio_test_<file-name>
VisioBridge.exe        -> VisioTestBridge.exe
```

Bridge 内部命令名如 `add_shape`、`preview` 保持不变；它们是本地 JSONL 私有协议，不与稳定版进程共享。公开 MCP 工具名必须全部重命名。

- [ ] **Step 4：运行测试和 smoke**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/tool-prefix.test.mjs editions/visio-scientific-illustrator-test/tests/node/server-routing.test.mjs
npm --prefix editions/visio-scientific-illustrator-test run smoke
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
```

Expected: PASS。

- [ ] **Step 5：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test editions/visio-scientific-illustrator-test/tests/node
git commit -m "feat(test-edition): isolate mcp names and tool prefix"
```

## Task 5：阶段回归与隔离审计

- [ ] 运行：

```powershell
npm --prefix editions/visio-scientific-illustrator-test test
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
git diff --check
```

- [ ] 搜索错误身份：

```powershell
Get-ChildItem -LiteralPath editions/visio-scientific-illustrator-test -Recurse -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'visio_live_|"visio-live"|"visio-file-utils"|VisioBridge\.exe|namespace VisioBridge'
```

Expected: 无匹配。私有文档中明确比较 Stable 与 Test 的表格不纳入此扫描。

- [ ] 确认 `git status --short` 不包含稳定版受保护文件和 `.neuralmemory/` 暂存项。

- [ ] 回归没有产生文件变化时不创建空提交；若修复了隔离问题，将修复文件与对应新增测试提交为 `fix(test-edition): repair isolation regression`。
