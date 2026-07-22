# Visio Scientific Illustrator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 将 draw.io 科研绘图 Codex 插件迁移为通过 C# COM Bridge 在可见 Microsoft Visio 窗口中逐步创建、检查、修改、保存和导出可编辑科研插图的 Windows 插件。

**Architecture:** 保留 Node.js MCP/JSON-RPC 外壳，实时 MCP 启动一个插件专属的 C# `VisioBridge.exe --mode live` 子进程，所有 COM 调用在单一 STA 线程串行执行。文件 MCP 使用同一 Bridge 的独立 `--mode file` 进程和 `Visio.InvisibleApp` 完成保存后验证与导出，避免干扰可见会话。

**Tech Stack:** Node.js 22 ESM、MCP JSON-RPC over stdio、.NET 8 `net8.0-windows`、C#、Microsoft Visio COM type library 4.10、Windows COM `IMessageFilter`、Node `node:test`、xUnit、PowerShell。

---

## 0. 执行约束与任务依赖

- 设计依据：`docs/superpowers/specs/2026-07-22-visio-scientific-illustrator-design.md`。
- 当前工作区尚不是 Git 仓库。Task 1 先导入上游仓库并建立迁移分支。
- 所有代码、注释和文本文件使用 UTF-8；代码注释使用中文。
- 禁止自动 fallback。缺少 Visio、Master、Shape、Cell 或能力时返回稳定错误码。
- 不接管用户现有 Visio 进程；第一版只控制插件自己创建的实例。
- 不直接修改 VSDX ZIP/XML；所有绘图与修改走 Visio COM。
- 不并行执行 COM 请求；Node 队列和 C# STA 队列同时生效。
- 关键路径：Task 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16 → 17。
- Task 18 是最终安装、人工验收和发布门禁。

## 1. 最终文件结构映射

### 仓库根目录

- `.agents/plugins/marketplace.json`：Visio marketplace 与插件入口。
- `package.json`：Node 语法检查、单元测试、Bridge 构建和总测试命令。
- `install.ps1`：Windows 环境检查与 Codex 插件安装。
- `README.md`：安装、使用、架构、限制和故障排查。
- `PRIVACY.md`：本地数据、进程和临时文件边界。
- `.github/workflows/ci.yml`：Node 跨平台检查和 Windows Bridge 测试。

### 插件目录

- `plugins/visio-scientific-illustrator/.codex-plugin/plugin.json`：Codex 插件元数据。
- `plugins/visio-scientific-illustrator/.mcp.json`：`visio-live` 与 `visio-file-utils` 两个 MCP 服务。
- `plugins/visio-scientific-illustrator/scripts/mcp-common.mjs`：MCP 帧、结果、错误、参数校验和请求队列。
- `plugins/visio-scientific-illustrator/scripts/bridge-client.mjs`：启动 Bridge、ready 握手、请求关联、超时和退出处理。
- `plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs`：工具 JSON Schema 的唯一来源。
- `plugins/visio-scientific-illustrator/scripts/live-server.mjs`：实时工具路由、节奏和 MCP 图片返回。
- `plugins/visio-scientific-illustrator/scripts/file-server.mjs`：文件验证、检查和导出路由。
- `plugins/visio-scientific-illustrator/assets/visio-scientific-masters.vssx`：复杂 Shape 的通用 Master；Task 15 才接入，基础闭环不依赖它。
- `plugins/visio-scientific-illustrator/bin/win-x64/VisioBridge.exe`：发布构建产物，不在普通源码提交中手工编辑。

### C# Bridge

- `plugins/visio-scientific-illustrator/bridge/VisioBridge.sln`：解决方案。
- `bridge/Directory.Build.props`：`net8.0-windows`、x64、nullable 和警告策略。
- `bridge/VisioBridge.Contracts/Protocol.cs`：请求、响应、错误和 ready DTO。
- `bridge/VisioBridge.Contracts/Commands.cs`：所有命令参数/结果 DTO。
- `bridge/VisioBridge.Core/BridgeErrorCodes.cs`：稳定错误码。
- `bridge/VisioBridge.Core/Geometry/GeometryConverter.cs`：像素/英寸与坐标系转换。
- `bridge/VisioBridge.Core/Session/VisioSession.cs`：owned Visio/Application/Document/Page 生命周期。
- `bridge/VisioBridge.Core/Session/StaCommandDispatcher.cs`：STA 线程、消息泵和串行队列。
- `bridge/VisioBridge.Core/Interop/OleMessageFilter.cs`：COM busy 有界重试。
- `bridge/VisioBridge.Core/Shapes/ShapeRepository.cs`：`User.CodexId` 和 Shape 重新解析。
- `bridge/VisioBridge.Core/Shapes/ShapeFactory.cs`：基础 Shape、文字、图片和 Master。
- `bridge/VisioBridge.Core/Shapes/StyleMapper.cs`：受控 ShapeSheet 映射。
- `bridge/VisioBridge.Core/Shapes/ConnectorService.cs`：1D Connector、GlueTo 和连接验证。
- `bridge/VisioBridge.Core/Inspection/PageInspector.cs`：页面、Shape 和连接清单。
- `bridge/VisioBridge.Core/Export/PreviewExporter.cs`：PNG 预览及设置恢复。
- `bridge/VisioBridge.Core/Files/SafeFileWriter.cs`：备份、验证、恢复和原子替换。
- `bridge/VisioBridge.Core/Abstractions/IDocumentSaver.cs`：安全保存状态机依赖的保存边界。
- `bridge/VisioBridge.Core/Abstractions/IDocumentValidator.cs`：保存完成后的独立验证边界。
- `bridge/VisioBridge.Core/Abstractions/IPreviewExportAdapter.cs`：预览设置快照、导出和恢复边界。
- `bridge/VisioBridge.Core/Files/VisioFileService.cs`：VSDX 保存、验证和导出。
- `bridge/VisioBridge.Core/Commands/CommandRouter.cs`：命令到服务的显式分派。
- `bridge/VisioBridge.Core/BridgeCommandException.cs`：稳定错误码异常，不把原始 COM 异常直接暴露为协议。
- `bridge/VisioBridge.Host/Program.cs`：JSON Lines 主循环和 host 模式。
- `bridge/VisioBridge.Tests/Fakes/**`：无 Visio 单元测试使用的显式假适配器。
- `bridge/VisioBridge.Tests/**`：无 Visio 单元测试和假适配器合约测试。
- `bridge/VisioBridge.IntegrationTests/VisioFixture.cs`：只创建和关闭测试 owned Visio 的共享夹具。
- `bridge/VisioBridge.IntegrationTests/**`：需要本机 Visio 的 Windows 集成测试。

### Skill

- `plugins/visio-scientific-illustrator/skills/recreate-scientific-figure-in-visio/SKILL.md`：Visio 专属工作流和硬边界。
- `plugins/visio-scientific-illustrator/skills/recreate-scientific-figure-in-visio/agents/openai.yaml`：显示名称、简介和默认提示。

---

### Task 1: 导入上游仓库并建立 Visio 迁移骨架

**Files:**
- Preserve: `docs/superpowers/specs/2026-07-22-visio-scientific-illustrator-design.md`
- Create: `docs/superpowers/plans/2026-07-22-visio-scientific-illustrator.md`
- Modify: `.agents/plugins/marketplace.json`
- Modify: `package.json`
- Move: `plugins/drawio-scientific-illustrator/` → `plugins/visio-scientific-illustrator/`
- Test: `scripts/validate-repo.mjs`

- [x] **Step 1: 初始化 Git 并导入上游 main**

Run:

```powershell
git init
git remote add upstream https://github.com/icebird1998/drawio-scientific-illustrator.git
git fetch upstream main
git checkout -b visio-migration upstream/main
```

Expected: 当前分支为 `visio-migration`；上游源码出现，现有 `docs/superpowers/**` 保持为未跟踪文件且未被覆盖。

- [x] **Step 2: 提交已经确认的设计与计划文档**

Run:

```powershell
git add docs/superpowers/specs/2026-07-22-visio-scientific-illustrator-design.md docs/superpowers/plans/2026-07-22-visio-scientific-illustrator.md
git commit -m "docs: add Visio migration design and plan"
```

Expected: 一个只包含两份文档的提交。

- [x] **Step 3: 重命名插件目录并更新 marketplace 名称**

Run:

```powershell
Move-Item -LiteralPath plugins/drawio-scientific-illustrator -Destination plugins/visio-scientific-illustrator
Move-Item -LiteralPath plugins/visio-scientific-illustrator/scripts/server.mjs -Destination plugins/visio-scientific-illustrator/scripts/file-server.mjs
```

Modify `.agents/plugins/marketplace.json` to exactly use:

```json
{
  "name": "visio-scientific-tools",
  "interface": {
    "displayName": "Visio Scientific Tools"
  },
  "plugins": [
    {
      "name": "visio-scientific-illustrator",
      "source": {
        "source": "local",
        "path": "./plugins/visio-scientific-illustrator"
      },
      "policy": {
        "installation": "AVAILABLE",
        "authentication": "ON_INSTALL"
      },
      "category": "Productivity"
    }
  ]
}
```

- [x] **Step 4: 写出会失败的仓库结构校验**

Modify `scripts/validate-repo.mjs` assertions to require:

```js
if (marketplace.name !== "visio-scientific-tools") throw new Error("Unexpected marketplace name.");
if (entry.name !== "visio-scientific-illustrator") throw new Error("Unexpected plugin name.");
if (manifest.version !== "0.1.0") throw new Error("Unexpected public release version.");
if (!mcp.mcpServers?.["visio-live"] || !mcp.mcpServers?.["visio-file-utils"]) {
  throw new Error("Required Visio MCP servers are missing.");
}
```

Change the validated script paths from `server.mjs` to `file-server.mjs`, and keep `live-server.mjs` in the required file list.

- [x] **Step 5: 运行校验并确认失败位置正确**

Run:

```powershell
node scripts/validate-repo.mjs
```

Expected: FAIL，错误为 `Unexpected plugin name` 或 `Required Visio MCP servers are missing`，因为 manifest 和 `.mcp.json` 尚未迁移。

- [x] **Step 6: 更新最小 manifest 与 MCP 名称使结构校验通过**

Modify `plugins/visio-scientific-illustrator/.codex-plugin/plugin.json` core fields:

```json
{
  "name": "visio-scientific-illustrator",
  "version": "0.1.0",
  "description": "Draw editable scientific figures live inside Microsoft Visio through a local COM bridge.",
  "license": "MIT",
  "keywords": ["visio", "scientific-figure", "diagram", "mcp"],
  "skills": "./skills/",
  "mcpServers": "./.mcp.json"
}
```

Modify `plugins/visio-scientific-illustrator/.mcp.json`:

```json
{
  "mcpServers": {
    "visio-live": {
      "command": "node",
      "args": ["./scripts/live-server.mjs"],
      "cwd": "."
    },
    "visio-file-utils": {
      "command": "node",
      "args": ["./scripts/file-server.mjs"],
      "cwd": "."
    }
  }
}
```

- [x] **Step 7: 更新 package 名称和检查入口**

Modify `package.json`:

```json
{
  "name": "visio-scientific-illustrator-marketplace",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "check": "node --check plugins/visio-scientific-illustrator/scripts/mcp-common.mjs && node --check plugins/visio-scientific-illustrator/scripts/bridge-client.mjs && node --check plugins/visio-scientific-illustrator/scripts/live-server.mjs && node --check plugins/visio-scientific-illustrator/scripts/file-server.mjs && node scripts/validate-repo.mjs",
    "test:node": "node --test tests/node/*.test.mjs",
    "build:bridge": "powershell -ExecutionPolicy Bypass -File scripts/build-bridge.ps1",
    "test:bridge": "dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.sln -c Release --filter \"Category!=VisioIntegration\"",
    "test": "npm run check && npm run test:node && npm run test:bridge"
  },
  "engines": {
    "node": ">=22"
  },
  "license": "MIT"
}
```

- [x] **Step 8: 运行结构校验**

Run:

```powershell
node scripts/validate-repo.mjs
```

Expected: PASS，输出 `Repository structure and portability checks passed.`

- [x] **Step 9: 提交骨架迁移**

```powershell
git add .agents package.json plugins scripts/validate-repo.mjs
git commit -m "chore: establish Visio plugin skeleton"
```

---

### Task 2: 拆出可测试的 Node MCP 公共层和串行请求队列

**Files:**
- Create: `plugins/visio-scientific-illustrator/scripts/mcp-common.mjs`
- Create: `plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs`
- Create: `tests/node/mcp-common.test.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/live-server.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/file-server.mjs`
- Delete: `plugins/visio-scientific-illustrator/scripts/drawio-path.mjs`
- Modify: `scripts/smoke-test.mjs`

- [x] **Step 1: 写串行队列失败测试**

Create `tests/node/mcp-common.test.mjs`:

```js
import test from "node:test";
import assert from "node:assert/strict";
import { SerialQueue, validateObject } from "../../plugins/visio-scientific-illustrator/scripts/mcp-common.mjs";

test("SerialQueue 按提交顺序完成状态修改", async () => {
  const queue = new SerialQueue();
  const order = [];
  const first = queue.run(async () => {
    await new Promise((resolve) => setTimeout(resolve, 20));
    order.push("first");
  });
  const second = queue.run(async () => order.push("second"));
  await Promise.all([first, second]);
  assert.deepEqual(order, ["first", "second"]);
});

test("validateObject 拒绝未知字段", () => {
  assert.throws(
    () => validateObject({ mode: "new", extra: true }, { required: ["mode"], allowed: ["mode"] }),
    /未知字段: extra/,
  );
});
```

- [x] **Step 2: 运行测试确认失败**

Run:

```powershell
node --test tests/node/mcp-common.test.mjs
```

Expected: FAIL，提示无法找到 `mcp-common.mjs` 或导出成员。

- [x] **Step 3: 实现公共 MCP 帧与串行队列**

Create `plugins/visio-scientific-illustrator/scripts/mcp-common.mjs` with these exported interfaces:

```js
export class SerialQueue {
  #tail = Promise.resolve();

  run(task) {
    const result = this.#tail.then(task, task);
    this.#tail = result.catch(() => undefined);
    return result;
  }
}

export function validateObject(value, { required = [], allowed = [] }) {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("参数必须是对象。");
  for (const key of required) if (!(key in value)) throw new Error(`缺少字段: ${key}`);
  for (const key of Object.keys(value)) if (!allowed.includes(key)) throw new Error(`未知字段: ${key}`);
  return value;
}

export function rpcResult(id, result) {
  return { jsonrpc: "2.0", id, result };
}

export function rpcError(id, code, message, data) {
  return { jsonrpc: "2.0", id: id ?? null, error: { code, message, ...(data === undefined ? {} : { data }) } };
}

export function toolResult(value, { imageData, isError = false } = {}) {
  const content = [{ type: "text", text: JSON.stringify(value, null, 2) }];
  if (imageData) content.push({ type: "image", data: imageData, mimeType: "image/png" });
  return { content, structuredContent: value, isError };
}
```

- [x] **Step 4: 运行 Node 单元测试**

Run:

```powershell
node --test tests/node/mcp-common.test.mjs
```

Expected: 2 tests PASS。

- [x] **Step 5: 建立工具 schema 唯一来源**

Create `tool-schemas.mjs` with an initial launch schema and explicit tool descriptor:

```js
export const launchSchema = {
  type: "object",
  required: ["mode"],
  properties: {
    mode: { type: "string", enum: ["new", "open"] },
    file_path: { type: "string" },
    page_name: { type: "string", default: "Figure" },
    step_delay_ms: { type: "integer", minimum: 100, maximum: 10000, default: 500 },
    maximize: { type: "boolean", default: true },
    include_preview: { type: "boolean", default: true }
  },
  additionalProperties: false
};

export const liveTools = [{
  name: "visio_live_launch",
  description: "Launch a plugin-owned visible Microsoft Visio document.",
  inputSchema: launchSchema,
}];
```

- [x] **Step 6: 将两个 MCP 入口改为公共处理器且不保留 draw.io 实现**

Replace the old Graph/XML code with thin MCP shells importing `mcp-common.mjs` and `tool-schemas.mjs`. Until Bridge exists, `tools/call` must return:

```js
toolResult({
  error: "BRIDGE_NOT_BUILT",
  message: "VisioBridge 尚未构建。",
  tool: params?.name,
}, { isError: true });
```

Delete `drawio-path.mjs`; Visio availability is detected by the C# COM Bridge, not by executable path scanning.

- [x] **Step 7: 运行语法检查和 smoke test**

Run:

```powershell
npm run check
node scripts/smoke-test.mjs
```

Expected: 两个 MCP 都能 initialize 和 tools/list；不得出现 draw.io 工具名。

- [x] **Step 8: 提交 Node MCP 公共层**

```powershell
git add plugins/visio-scientific-illustrator/scripts tests/node scripts/smoke-test.mjs
git commit -m "refactor: add serialized Visio MCP foundation"
```

---

### Task 3: 建立 C# Solution、协议 DTO 和 JSON Lines Host

**Files:**
- Create: `plugins/visio-scientific-illustrator/bridge/VisioBridge.sln`
- Create: `plugins/visio-scientific-illustrator/bridge/Directory.Build.props`
- Create: `bridge/VisioBridge.Contracts/VisioBridge.Contracts.csproj`
- Create: `bridge/VisioBridge.Contracts/Protocol.cs`
- Create: `bridge/VisioBridge.Contracts/Commands.cs`
- Create: `bridge/VisioBridge.Core/VisioBridge.Core.csproj`
- Create: `bridge/VisioBridge.Core/BridgeErrorCodes.cs`
- Create: `bridge/VisioBridge.Core/BridgeCommandException.cs`
- Create: `bridge/VisioBridge.Host/VisioBridge.Host.csproj`
- Create: `bridge/VisioBridge.Host/Program.cs`
- Create: `bridge/VisioBridge.Tests/VisioBridge.Tests.csproj`
- Create: `bridge/VisioBridge.Tests/ProtocolTests.cs`

- [x] **Step 1: 创建解决方案和项目**

Run from `plugins/visio-scientific-illustrator/bridge`:

```powershell
dotnet new sln -n VisioBridge
dotnet new classlib -n VisioBridge.Contracts -f net8.0
dotnet new classlib -n VisioBridge.Core -f net8.0-windows
dotnet new console -n VisioBridge.Host -f net8.0-windows
dotnet new xunit -n VisioBridge.Tests -f net8.0-windows
dotnet sln add VisioBridge.Contracts/VisioBridge.Contracts.csproj VisioBridge.Core/VisioBridge.Core.csproj VisioBridge.Host/VisioBridge.Host.csproj VisioBridge.Tests/VisioBridge.Tests.csproj
dotnet add VisioBridge.Core/VisioBridge.Core.csproj reference VisioBridge.Contracts/VisioBridge.Contracts.csproj
dotnet add VisioBridge.Host/VisioBridge.Host.csproj reference VisioBridge.Core/VisioBridge.Core.csproj VisioBridge.Contracts/VisioBridge.Contracts.csproj
dotnet add VisioBridge.Tests/VisioBridge.Tests.csproj reference VisioBridge.Core/VisioBridge.Core.csproj VisioBridge.Contracts/VisioBridge.Contracts.csproj
```

Expected: Solution contains four projects and restores successfully.

- [x] **Step 2: 固定 Windows/x64/nullable 构建规则**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
</Project>
```

- [x] **Step 3: 写协议序列化失败测试**

Create `VisioBridge.Tests/ProtocolTests.cs`:

```csharp
using System.Text.Json;
using VisioBridge.Contracts;

namespace VisioBridge.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void Request_round_trips_with_command_and_id()
    {
        const string json = """{"id":7,"command":"status","params":{}}""";
        var request = JsonSerializer.Deserialize<BridgeRequest>(json, ProtocolJson.Options);
        Assert.NotNull(request);
        Assert.Equal(7, request.Id);
        Assert.Equal("status", request.Command);
    }
}
```

- [x] **Step 4: 运行测试确认失败**

Run:

```powershell
dotnet test VisioBridge.sln -c Release
```

Expected: FAIL，`BridgeRequest` 或 `ProtocolJson` 不存在。

- [x] **Step 5: 实现协议 DTO**

Create `VisioBridge.Contracts/Protocol.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisioBridge.Contracts;

public sealed record BridgeRequest(long Id, string Command, JsonElement Params);
public sealed record BridgeFailure(string Code, string Message, bool Retryable, object? Details = null);
public sealed record BridgeResponse(long Id, bool Ok, object? Result = null, BridgeFailure? Error = null);
public sealed record BridgeReady(string Type, string BridgeVersion, int ProcessId, string Architecture);

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
```

Create `BridgeErrorCodes.cs` with constants:

```csharp
namespace VisioBridge.Core;

public static class BridgeErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string UnsupportedOperation = "UNSUPPORTED_OPERATION";
    public const string InternalError = "INTERNAL_ERROR";
    public const string VisioNotInstalled = "VISIO_NOT_INSTALLED";
    public const string VisioStartFailed = "VISIO_START_FAILED";
    public const string VisioBusy = "VISIO_BUSY";
    public const string SessionNotReady = "SESSION_NOT_READY";
    public const string SessionInvalidated = "SESSION_INVALIDATED";
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";
    public const string PageNotFound = "PAGE_NOT_FOUND";
    public const string ShapeNotFound = "SHAPE_NOT_FOUND";
    public const string DuplicateSemanticId = "DUPLICATE_SEMANTIC_ID";
    public const string MasterNotFound = "MASTER_NOT_FOUND";
    public const string CellNotFound = "CELL_NOT_FOUND";
    public const string ConnectorNotGlued = "CONNECTOR_NOT_GLUED";
    public const string InvalidCoordinates = "INVALID_COORDINATES";
    public const string InvalidStyle = "INVALID_STYLE";
    public const string OutputExists = "OUTPUT_EXISTS";
    public const string SavePathRequired = "SAVE_PATH_REQUIRED";
    public const string SaveFailed = "SAVE_FAILED";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ExportFailed = "EXPORT_FAILED";
}
```

Create `BridgeCommandException.cs`:

```csharp
namespace VisioBridge.Core;

public sealed class BridgeCommandException : Exception
{
    public BridgeCommandException(string code, string message, bool retryable = false, object? details = null)
        : base(message)
    {
        Code = code;
        Retryable = retryable;
        Details = details;
    }

    public string Code { get; }
    public bool Retryable { get; }
    public object? Details { get; }

    public static BridgeCommandException Unsupported(string message) =>
        new(BridgeErrorCodes.UnsupportedOperation, message);
}
```

- [x] **Step 6: 实现最小 status Host**

Create `VisioBridge.Host/Program.cs` with a line loop that writes one ready line, accepts only `status`, and writes structured errors for other commands:

```csharp
using System.Text.Json;
using VisioBridge.Contracts;
using VisioBridge.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine(JsonSerializer.Serialize(
    new BridgeReady("ready", "0.1.0", Environment.ProcessId, RuntimeInformation.ProcessArchitecture.ToString()),
    ProtocolJson.Options));

string? line;
while ((line = Console.ReadLine()) is not null)
{
    BridgeResponse response;
    try
    {
        var request = JsonSerializer.Deserialize<BridgeRequest>(line, ProtocolJson.Options)
            ?? throw new JsonException("请求为空。");
        response = request.Command == "status"
            ? new BridgeResponse(request.Id, true, new { state = "stopped" })
            : new BridgeResponse(request.Id, false, Error: new(
                BridgeErrorCodes.UnsupportedOperation,
                $"不支持命令: {request.Command}",
                false));
    }
    catch (Exception error)
    {
        response = new BridgeResponse(0, false, Error: new(
            BridgeErrorCodes.InvalidRequest,
            error.Message,
            false));
    }
    Console.WriteLine(JsonSerializer.Serialize(response, ProtocolJson.Options));
}
```

Also add `using System.Runtime.InteropServices;` and remove generated `Class1.cs` files.

- [x] **Step 7: 运行 Bridge 测试与手工协议探测**

Run:

```powershell
dotnet test VisioBridge.sln -c Release
'{"id":1,"command":"status","params":{}}' | dotnet run --project VisioBridge.Host/VisioBridge.Host.csproj -c Release
```

Expected: tests PASS；stdout 第一行是 `type=ready`，第二行是 `id=1, ok=true, state=stopped`。

- [x] **Step 8: 提交 Bridge 协议骨架**

```powershell
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: add Visio bridge protocol host"
```

---

### Task 4: 实现 Node BridgeClient、ready 握手和超时处理

**Files:**
- Create: `plugins/visio-scientific-illustrator/scripts/bridge-client.mjs`
- Create: `tests/node/bridge-client.test.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/live-server.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/file-server.mjs`

- [x] **Step 1: 写 fake bridge fixture**

Create `tests/node/fake-bridge.mjs`:

```js
import { createInterface } from "node:readline";

process.stdout.write(`${JSON.stringify({ type: "ready", bridgeVersion: "test", processId: process.pid, architecture: "X64" })}\n`);
const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
lines.on("line", (line) => {
  const request = JSON.parse(line);
  if (request.command === "hang") return;
  process.stdout.write(`${JSON.stringify({ id: request.id, ok: true, result: { command: request.command } })}\n`);
});
```

- [x] **Step 2: 写握手、关联和超时失败测试**

Create `tests/node/bridge-client.test.mjs`:

```js
import test from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { BridgeClient } from "../../plugins/visio-scientific-illustrator/scripts/bridge-client.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));

test("BridgeClient waits for ready and correlates responses", async () => {
  const client = new BridgeClient(process.execPath, [path.join(here, "fake-bridge.mjs")]);
  await client.start();
  const result = await client.call("status", {}, 1000);
  assert.equal(result.command, "status");
  await client.stop();
});

test("BridgeClient rejects timed out calls", async () => {
  const client = new BridgeClient(process.execPath, [path.join(here, "fake-bridge.mjs")]);
  await client.start();
  await assert.rejects(() => client.call("hang", {}, 20), /BRIDGE_TIMEOUT/);
  await client.stop();
});
```

- [x] **Step 3: 运行测试确认失败**

```powershell
node --test tests/node/bridge-client.test.mjs
```

Expected: FAIL，`BridgeClient` 尚不存在。

- [x] **Step 4: 实现 BridgeClient**

`bridge-client.mjs` must:

```js
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";

export class BridgeClient {
  constructor(command, args, { cwd, readyTimeoutMs = 10000 } = {}) {
    this.command = command;
    this.args = args;
    this.cwd = cwd;
    this.readyTimeoutMs = readyTimeoutMs;
    this.nextId = 1;
    this.pending = new Map();
    this.child = null;
    this.ready = null;
  }

  async start() {
    if (this.child) throw new Error("BRIDGE_ALREADY_STARTED");
    this.child = spawn(this.command, this.args, { cwd: this.cwd, stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
    this.child.stderr.on("data", (chunk) => process.stderr.write(chunk));
    const lines = createInterface({ input: this.child.stdout, crlfDelay: Infinity });
    this.lines = lines;

    const ready = new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error("BRIDGE_READY_TIMEOUT")), this.readyTimeoutMs);
      this.resolveReady = (value) => { clearTimeout(timer); resolve(value); };
      this.rejectReady = (error) => { clearTimeout(timer); reject(error); };
    });

    lines.on("line", (line) => this.#handleLine(line));
    this.child.once("error", (error) => this.#failAll(new Error(`BRIDGE_START_FAILED: ${error.message}`)));
    this.child.once("exit", (code) => this.#failAll(new Error(`BRIDGE_EXITED: ${code}`)));
    this.ready = await ready;
    return this.ready;
  }

  call(command, params, timeoutMs = 30000) {
    if (!this.child || !this.ready) return Promise.reject(new Error("BRIDGE_NOT_READY"));
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`BRIDGE_TIMEOUT: ${command}`));
      }, timeoutMs);
      this.pending.set(id, {
        resolve: (value) => { clearTimeout(timer); resolve(value); },
        reject: (error) => { clearTimeout(timer); reject(error); },
      });
      this.child.stdin.write(`${JSON.stringify({ id, command, params })}\n`);
    });
  }

  async stop() {
    if (!this.child) return;
    const child = this.child;
    child.stdin.end();
    await Promise.race([
      new Promise((resolve) => child.once("exit", resolve)),
      new Promise((resolve) => setTimeout(resolve, 2000)),
    ]);
    if (child.exitCode === null) child.kill();
    this.lines?.close();
    this.#failAll(new Error("BRIDGE_STOPPED"));
    this.child = null;
    this.ready = null;
  }

  #handleLine(line) {
    let message;
    try { message = JSON.parse(line); }
    catch { this.#failAll(new Error("BRIDGE_PROTOCOL_ERROR")); return; }
    if (message.type === "ready" && !this.ready) { this.resolveReady(message); return; }
    const pending = this.pending.get(message.id);
    if (!pending) return;
    this.pending.delete(message.id);
    if (message.ok) pending.resolve(message.result);
    else pending.reject(Object.assign(new Error(message.error?.message || "BRIDGE_ERROR"), { bridgeError: message.error }));
  }

  #failAll(error) {
    this.rejectReady?.(error);
    for (const pending of this.pending.values()) pending.reject(error);
    this.pending.clear();
  }
}
```

Required behavior:

- stdout 非 JSON：拒绝所有 pending，错误 `BRIDGE_PROTOCOL_ERROR`。
- Bridge exit：拒绝所有 pending，错误 `BRIDGE_EXITED`。
- timeout：只拒绝当前调用，不自动重启 Bridge。
- stderr：转发到 MCP stderr，不能进入 MCP stdout。
- `stop()` clears pending timers and listeners.

Bridge executable resolution must be explicit:

```js
export function resolveBridgeExecutable(pluginRoot) {
  const configured = process.env.VISIO_BRIDGE_PATH?.trim();
  const candidate = configured || path.join(pluginRoot, "bin", "win-x64", "VisioBridge.exe");
  if (!path.isAbsolute(candidate) || !existsSync(candidate)) {
    throw new Error(`BRIDGE_NOT_FOUND: ${candidate}`);
  }
  return candidate;
}
```

Do not search PATH or choose another binary when the explicit/bundled path is missing.

- [x] **Step 5: 运行 Node tests**

```powershell
node --test tests/node/bridge-client.test.mjs
```

Expected: 2 tests PASS，无残留 node 子进程。

- [x] **Step 6: 将 MCP status 调用接到 Bridge**

`live-server.mjs` creates one lazily started client and routes `visio_live_status` to `client.call("status", {})`. `file-server.mjs` creates a file-mode client only when its first tool is called.

- [x] **Step 7: 运行 MCP smoke test**

```powershell
npm run check
node scripts/smoke-test.mjs
```

Expected: initialize/tools/list PASS；smoke test 不要求本机启动 Visio。

- [x] **Step 8: 提交 Bridge client**

```powershell
git add plugins/visio-scientific-illustrator/scripts tests/node
git commit -m "feat: connect MCP servers to Visio bridge"
```

---

### Task 5: 实现 STA Dispatcher、COM 引用和插件专属 Visio 会话

**Files:**
- Modify: `bridge/VisioBridge.Core/VisioBridge.Core.csproj`
- Create: `bridge/VisioBridge.Core/Session/StaCommandDispatcher.cs`
- Create: `bridge/VisioBridge.Core/Session/VisioSession.cs`
- Create: `bridge/VisioBridge.Core/Session/SessionState.cs`
- Create: `bridge/VisioBridge.Core/Abstractions/IVisioSession.cs`
- Create: `bridge/VisioBridge.Core/Commands/CommandRouter.cs`
- Create: `bridge/VisioBridge.Tests/StaCommandDispatcherTests.cs`
- Create: `bridge/VisioBridge.IntegrationTests/SessionIntegrationTests.cs`
- Create: `bridge/VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj`
- Create: `bridge/VisioBridge.IntegrationTests/VisioFixture.cs`

- [x] **Step 1: 创建 integration test project 并加入 Solution**

Run from `plugins/visio-scientific-illustrator/bridge`:

```powershell
dotnet new xunit -n VisioBridge.IntegrationTests -f net8.0-windows
dotnet add VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj reference VisioBridge.Core/VisioBridge.Core.csproj VisioBridge.Contracts/VisioBridge.Contracts.csproj
dotnet sln add VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj
```

Create `VisioFixture.cs` with this ownership contract:

```csharp
public sealed class VisioFixture : IDisposable
{
    public static VisioFixture StartVisible();
    public CommandRouter Router { get; }
    public void Dispose(); // 只关闭 fixture 创建并记录的 owned PID
}
```

- [x] **Step 2: 添加 Visio COMReference**

Modify `VisioBridge.Core.csproj`:

```xml
<PropertyGroup>
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\VisioBridge.Contracts\VisioBridge.Contracts.csproj" />
  <COMReference Include="Microsoft.Office.Interop.Visio">
    <Guid>{00021A98-0000-0000-C000-000000000046}</Guid>
    <VersionMajor>4</VersionMajor>
    <VersionMinor>10</VersionMinor>
    <Lcid>0</Lcid>
    <WrapperTool>tlbimp</WrapperTool>
    <EmbedInteropTypes>true</EmbedInteropTypes>
  </COMReference>
</ItemGroup>
```

Expected: build machine must have Visio type library 4.10 registered; absence is a build error, not a fallback to late binding.

- [x] **Step 3: 写 STA 串行测试**

Create `StaCommandDispatcherTests.cs`:

```csharp
using VisioBridge.Core.Session;

namespace VisioBridge.Tests;

public sealed class StaCommandDispatcherTests
{
    [Fact]
    public async Task Commands_run_on_one_STA_thread_in_order()
    {
        using var dispatcher = new StaCommandDispatcher();
        var calls = new List<(int ThreadId, ApartmentState State, int Value)>();
        var first = dispatcher.InvokeAsync(() => calls.Add((Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState(), 1)));
        var second = dispatcher.InvokeAsync(() => calls.Add((Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState(), 2)));
        await Task.WhenAll(first, second);
        Assert.Equal(new[] { 1, 2 }, calls.Select(x => x.Value));
        Assert.Single(calls.Select(x => x.ThreadId).Distinct());
        Assert.All(calls, x => Assert.Equal(ApartmentState.STA, x.State));
    }
}
```

- [x] **Step 4: 运行测试确认失败**

```powershell
dotnet test VisioBridge.sln -c Release --filter StaCommandDispatcherTests
```

Expected: FAIL，类不存在。

- [x] **Step 5: 实现 STA dispatcher**

`StaCommandDispatcher` must create one background thread, call `SetApartmentState(ApartmentState.STA)` before start, create a hidden Windows Forms `Control`, and run `Application.Run()` on that thread. `Control.BeginInvoke` provides FIFO dispatch while the Windows message loop remains active for Visio COM reentrancy.

Public surface:

```csharp
public sealed class StaCommandDispatcher : IDisposable
{
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
    public void Dispose();
}
```

The implementation core must follow this structure:

```csharp
private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
private readonly Thread _thread;
private Control _control = null!;
private bool _disposed;

public StaCommandDispatcher()
{
    _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Visio COM STA" };
    _thread.SetApartmentState(ApartmentState.STA);
    _thread.Start();
    _ready.Task.GetAwaiter().GetResult();
}

private void ThreadMain()
{
    _control = new Control();
    _control.CreateControl();
    _ready.SetResult();
    Application.Run();
    _control.Dispose();
}

public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
{
    var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    cancellationToken.ThrowIfCancellationRequested();
    _control.BeginInvoke(new Action(() =>
    {
        if (cancellationToken.IsCancellationRequested) { completion.SetCanceled(cancellationToken); return; }
        try { completion.SetResult(action()); }
        catch (Exception error) { completion.SetException(error); }
    }));
    return completion.Task;
}

public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _control.BeginInvoke(new Action(Application.ExitThread));
    if (!_thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Visio COM STA 线程未在 5 秒内退出。");
}
```

- [x] **Step 6: 运行 STA tests**

```powershell
dotnet test VisioBridge.sln -c Release --filter StaCommandDispatcherTests
```

Expected: PASS。

- [x] **Step 7: 定义会话状态与接口**

Create:

```csharp
public enum SessionState { Stopped, Starting, Ready, Invalidated, Faulted }

public sealed record CanvasSpec(int WidthPx, int HeightPx, int Dpi);

public sealed record SessionStatus(
    SessionState State,
    int? ProcessId,
    bool Visible,
    string? DocumentName,
    string? PageName,
    int ShapeCount,
    int ConnectorCount,
    bool Dirty);

public sealed record LaunchResult(
    string SessionId,
    int ProcessId,
    bool Visible,
    string VisioVersion,
    string DocumentName,
    string PageName,
    CanvasSpec Canvas,
    string SessionTempRoot);

public interface IVisioSession : IDisposable
{
    SessionState State { get; }
    int? OwnedProcessId { get; }
    SessionStatus GetStatus();
    LaunchResult LaunchNew(string pageName, int widthPx, int heightPx, int dpi, bool maximize);
    LaunchResult Open(string absoluteVsDxPath, int widthPx, int heightPx, int dpi, bool maximize);
    void CloseDocument(string action); // action 仅允许 save、discard、cancel
    void Shutdown(bool confirm, string dirtyAction); // dirtyAction 仅允许 save、discard、cancel
}
```

- [x] **Step 8: 实现 owned Visio session**

`VisioSession` requirements:

- Instantiate `Microsoft.Office.Interop.Visio.InvisibleApp` on the dispatcher STA thread.
- Set `Visible = true` only after the document/page is ready.
- For new mode call `Documents.Add("")`.
- For open mode require an existing absolute `.vsdx` and call `Documents.Open`.
- Store `Application.ProcessID`, document identity and bound page ID.
- Set page name and PageWidth/PageHeight using `CellsU`/`FormulaU`.
- Do not use `GetActiveObject` or attach to another process.
- `CloseDocument("cancel")` performs no mutation and reports `cancelled=true`.
- `CloseDocument("save")` calls `Document.Save`; an unsaved document without a prior `visio_live_save` returns `SAVE_PATH_REQUIRED`.
- `CloseDocument("discard")` may set `Document.Saved=true` only because the caller explicitly requested data loss, then calls `Document.Close` without a modal prompt.
- `Shutdown` first applies the explicit dirty action, then calls `Application.Quit` only for the owned instance; a cancelled close leaves Visio running.
- Add `CreateForIntegrationTest()` as an internal factory exposed to the integration-test assembly; it creates the same owned session path and does not attach to an existing process.

- [x] **Step 9: 写仅 Windows/Visio 环境运行的 integration test**

Create integration test:

```csharp
[Trait("Category", "VisioIntegration")]
[Fact]
public void LaunchNew_creates_visible_owned_session()
{
    using var session = VisioSession.CreateForIntegrationTest();
    var result = session.LaunchNew("Figure", 1600, 1000, 96, true);
    Assert.True(result.Visible);
    Assert.True(result.ProcessId > 0);
    Assert.Equal(SessionState.Ready, session.State);
    session.Shutdown(true);
}
```

- [x] **Step 10: 运行单元测试和本机 integration test**

```powershell
dotnet test VisioBridge.sln -c Release --filter "Category!=VisioIntegration"
dotnet test VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj -c Release --filter "Category=VisioIntegration"
```

Expected: 第一条 PASS；第二条打开可见 Visio，随后只关闭测试创建的进程并 PASS。

- [x] **Step 11: 将 launch/status/shutdown 接到 CommandRouter**

Commands:

```text
launch_new
open_document
status
shutdown
```

Unknown commands must return `UNSUPPORTED_OPERATION`.

- [x] **Step 12: 提交会话闭环**

```powershell
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: launch plugin-owned Visio sessions"
```

---

### Task 6: 实现坐标转换和页面尺寸

**Files:**
- Create: `bridge/VisioBridge.Core/Geometry/GeometryConverter.cs`
- Create: `bridge/VisioBridge.Contracts/Geometry.cs`
- Create: `bridge/VisioBridge.Tests/GeometryConverterTests.cs`
- Modify: `bridge/VisioBridge.Core/Session/VisioSession.cs`

- [x] **Step 1: 写坐标正反转换失败测试**

```csharp
[Theory]
[InlineData(0, 0, 96, 96, 0.5, 9.9166666667)]
[InlineData(120, 80, 240, 100, 2.5, 9.0625)]
public void TopLeftPixels_convert_to_Visio_pin(
    double x, double y, double width, double height, double expectedPinX, double expectedPinY)
{
    var converter = new GeometryConverter(new CanvasSpec(1600, 1000, 96));
    var box = converter.ToVisio(new PixelBox(x, y, width, height));
    Assert.Equal(expectedPinX, box.PinXInches, 8);
    Assert.Equal(expectedPinY, box.PinYInches, 8);
    var roundTrip = converter.ToPixels(box);
    Assert.Equal(x, roundTrip.X, 8);
    Assert.Equal(y, roundTrip.Y, 8);
    Assert.Equal(width, roundTrip.Width, 8);
    Assert.Equal(height, roundTrip.Height, 8);
}
```

- [x] **Step 2: 运行测试确认失败**

```powershell
dotnet test VisioBridge.sln -c Release --filter GeometryConverterTests
```

Expected: FAIL，几何类型不存在。

- [x] **Step 3: 实现不可变几何 DTO 和 converter**

Required types:

```csharp
public sealed record PixelPoint(double X, double Y);
public sealed record PixelBox(double X, double Y, double Width, double Height);
public sealed record VisioBox(double PinXInches, double PinYInches, double WidthInches, double HeightInches);
```

Required formulas:

```csharp
var pinX = (box.X + box.Width / 2d) / canvas.Dpi;
var pinY = (canvas.HeightPx - box.Y - box.Height / 2d) / canvas.Dpi;
```

Reject non-finite numbers, non-positive dimensions, boxes outside a configurable canvas margin, and DPI outside 48–600.

- [x] **Step 4: 运行几何测试**

```powershell
dotnet test VisioBridge.sln -c Release --filter GeometryConverterTests
```

Expected: PASS，包括边界、逆变换和非法数值测试。

- [x] **Step 5: 让 VisioSession 使用同一 CanvasSpec 设置页面**

Set:

```csharp
page.PageSheet.CellsU["PageWidth"].FormulaU = $"{widthInches.ToString(CultureInfo.InvariantCulture)} in";
page.PageSheet.CellsU["PageHeight"].FormulaU = $"{heightInches.ToString(CultureInfo.InvariantCulture)} in";
```

- [x] **Step 6: 运行所有非集成测试并提交**

```powershell
dotnet test VisioBridge.sln -c Release --filter "Category!=VisioIntegration"
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: add Visio page geometry conversion"
```

---

### Task 7: 实现稳定语义 ID 和 ShapeRepository

**Files:**
- Create: `bridge/VisioBridge.Core/Shapes/ShapeRepository.cs`
- Create: `bridge/VisioBridge.Core/Abstractions/IShapeAccessor.cs`
- Create: `bridge/VisioBridge.Tests/ShapeRepositoryTests.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/FakeShapeAccessor.cs`

- [x] **Step 1: 用假 Shape accessor 写查找和重复 ID 测试**

```csharp
[Fact]
public void Resolve_rejects_duplicate_semantic_ids()
{
    var accessor = FakeShapeAccessor.WithShapes(
        new FakeShape(1, "encoder"),
        new FakeShape(2, "encoder"));
    var repository = new ShapeRepository(accessor);
    var error = Assert.Throws<BridgeCommandException>(() => repository.Resolve("encoder"));
    Assert.Equal(BridgeErrorCodes.DuplicateSemanticId, error.Code);
}

[Fact]
public void Register_writes_User_CodexId_and_NameU()
{
    var shape = new FakeShape(3, null);
    var repository = new ShapeRepository(FakeShapeAccessor.WithShapes(shape));
    repository.Register(shape, "encoder block");
    Assert.Equal("encoder block", shape.UserCodexId);
    Assert.Equal("Codex_encoder_block", shape.NameU);
}
```

- [x] **Step 2: 运行测试确认失败**

```powershell
dotnet test VisioBridge.sln -c Release --filter ShapeRepositoryTests
```

Expected: FAIL，repository 和 fake adapter 契约不存在。

- [x] **Step 3: 定义 Shape accessor 边界**

```csharp
public interface IShapeAccessor
{
    IReadOnlyList<IShapeHandle> EnumerateBoundPageShapesRecursively();
    string? ReadSemanticId(IShapeHandle shape);
    void WriteSemanticId(IShapeHandle shape, string semanticId);
    void SetUniversalName(IShapeHandle shape, string nameU);
}

public interface IShapeHandle
{
    int Id { get; }
    string? UserCodexId { get; set; }
    string NameU { get; set; }
}
```

Visio adapter must add `visSectionUser` and a named row `CodexId` only when absent, then set `User.CodexId` to a quoted string formula.

Create `FakeShapeAccessor.cs` with the exact members used by the tests:

```csharp
internal sealed class FakeShape : IShapeHandle
{
    public FakeShape(int id, string? userCodexId) { Id = id; UserCodexId = userCodexId; }
    public int Id { get; }
    public string? UserCodexId { get; set; }
    public string NameU { get; set; } = string.Empty;
}

internal sealed class FakeShapeAccessor : IShapeAccessor
{
    public static FakeShapeAccessor WithShapes(params FakeShape[] shapes) => new(shapes);
    // Enumerate、Read、Write 和 SetUniversalName 只操作传入集合，不访问 COM。
}
```

- [x] **Step 4: 实现明确的规范化规则**

`NameU` generation:

```text
semantic id 原文保存在 User.CodexId
NameU = "Codex_" + 将非 ASCII 字母数字下划线替换为下划线后的值
NameU 冲突时不追加随机后缀；返回 DUPLICATE_SEMANTIC_ID
```

- [x] **Step 5: 实现每次重新枚举的 Resolve**

No long-lived Shape RCW cache. `Resolve` enumerates the bound page, matches exact `User.CodexId`, returns zero/one/multiple as `SHAPE_NOT_FOUND`/success/`DUPLICATE_SEMANTIC_ID`.

- [x] **Step 6: 运行 tests 并提交**

```powershell
dotnet test VisioBridge.sln -c Release --filter ShapeRepositoryTests
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: track Visio shapes by stable semantic id"
```

---

### Task 8: 创建基础 Shape、文字、图片与受控样式映射

**Files:**
- Create: `bridge/VisioBridge.Core/Shapes/ShapeFactory.cs`
- Create: `bridge/VisioBridge.Core/Shapes/StyleMapper.cs`
- Create: `bridge/VisioBridge.Contracts/ShapeCommands.cs`
- Create: `bridge/VisioBridge.Tests/StyleMapperTests.cs`
- Create: `bridge/VisioBridge.IntegrationTests/ShapeIntegrationTests.cs`
- Modify: `bridge/VisioBridge.IntegrationTests/VisioFixture.cs`
- Modify: `bridge/VisioBridge.Core/Commands/CommandRouter.cs`

- [x] **Step 1: 写颜色、单位和非法样式测试**

```csharp
[Theory]
[InlineData("#112233", "RGB(17,34,51)")]
[InlineData("#FFFFFF", "RGB(255,255,255)")]
public void Hex_colors_become_controlled_RGB_formulas(string input, string expected)
{
    Assert.Equal(expected, StyleMapper.ToRgbFormula(input));
}

[Theory]
[InlineData("red")]
[InlineData("#12345")]
[InlineData("#GG0000")]
public void Invalid_colors_are_rejected(string input)
{
    Assert.Throws<BridgeCommandException>(() => StyleMapper.ToRgbFormula(input));
}
```

- [x] **Step 2: 运行测试确认失败**

```powershell
dotnet test VisioBridge.sln -c Release --filter StyleMapperTests
```

Expected: FAIL。

- [x] **Step 3: 定义 AddShape 参数 DTO**

```csharp
public sealed record AddShapeCommand(
    string SemanticId,
    string Shape,
    PixelBox Bounds,
    string? Label,
    ShapeStyle? Style);

public sealed record ShapeStyle(
    string? FillColor,
    double? FillTransparency,
    string? StrokeColor,
    double? StrokeWidthPt,
    string? FontColor,
    double? FontSizePt,
    double? RotationDeg,
    bool? Dashed);
```

- [x] **Step 4: 实现 StyleMapper**

Map only these universal cells:

```text
FillForegnd, FillForegndTrans, LineColor, LineWeight, LinePattern,
Char.Color, Char.Size, Angle, Para.HorzAlign, VerticalAlign
```

Before each optional cell write, call `CellExistsU`. Missing required cell returns `CELL_NOT_FOUND`; do not skip silently.

- [x] **Step 5: 实现基础 ShapeFactory**

Dispatch table:

```csharp
return command.Shape switch
{
    "rectangle" => page.DrawRectangle(left, top, right, bottom),
    "ellipse" => page.DrawOval(left, top, right, bottom),
    "text" => CreateTextShape(page, box),
    _ => throw BridgeCommandException.Unsupported($"不支持基础 Shape: {command.Shape}"),
};
```

After creation:

1. Set geometry from `GeometryConverter`.
2. Assign `Shape.Text` when label is provided.
3. Apply controlled style.
4. Register semantic ID.
5. Return Shape ID, NameU, normalized geometry and style summary.

- [x] **Step 6: 实现显式图片命令**

`add_image` accepts absolute PNG/JPEG/SVG/EMF path, validates extension and existence, calls `Page.Import`, sets requested geometry, registers semantic ID, and returns `rasterized=true` for PNG/JPEG.

- [x] **Step 7: 写并运行 Visio shape integration test**

First extend `VisioFixture` with:

```csharp
public IShapeHandle Resolve(string semanticId);
public string ReadUserCodexId(IShapeHandle shape);
```

```csharp
[Trait("Category", "VisioIntegration")]
[Fact]
public void Add_rectangle_and_text_creates_editable_shapes()
{
    using var fixture = VisioFixture.StartVisible();
    fixture.Router.Execute("add_shape", new AddShapeCommand(
        "encoder", "rectangle", new PixelBox(100, 100, 240, 100), "Encoder",
        new ShapeStyle("#DAE8FC", 0, "#6C8EBF", 1.5, "#1F2937", 14, 0, false)));
    var shape = fixture.Resolve("encoder");
    Assert.Equal("Encoder", shape.Text);
    Assert.Equal("encoder", fixture.ReadUserCodexId(shape));
}
```

Run:

```powershell
dotnet test VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj -c Release --filter ShapeIntegrationTests
```

Expected: 可见 Visio 中出现可选择矩形与文字，test PASS。

- [x] **Step 8: 提交 Shape 核心**

```powershell
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: draw styled editable Visio shapes"
```

---

### Task 9: 实现真正胶合的 1D Connector

**Files:**
- Create: `bridge/VisioBridge.Core/Shapes/ConnectorService.cs`
- Create: `bridge/VisioBridge.Contracts/ConnectorCommands.cs`
- Create: `bridge/VisioBridge.Tests/ConnectorServiceTests.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/ConnectorFixture.cs`
- Create: `bridge/VisioBridge.IntegrationTests/ConnectorIntegrationTests.cs`
- Modify: `bridge/VisioBridge.IntegrationTests/VisioFixture.cs`
- Modify: `bridge/VisioBridge.Core/Commands/CommandRouter.cs`

- [x] **Step 1: 写缺失端点和重复 ID 测试**

```csharp
[Fact]
public void Connector_rejects_missing_source()
{
    var service = ConnectorFixture.CreateWithoutShapes();
    var error = Assert.Throws<BridgeCommandException>(() => service.Add(
        new AddConnectorCommand("flow", "missing", "target", null)));
    Assert.Equal(BridgeErrorCodes.ShapeNotFound, error.Code);
}
```

Create `ConnectorFixture.cs` as a fake page/repository boundary with these exact methods:

```csharp
internal sealed class ConnectorFixture
{
    public static ConnectorService CreateWithoutShapes();
    public static ConnectorService CreateWithShapes(params string[] semanticIds);
}
```

- [x] **Step 2: 定义 connector DTO**

```csharp
public sealed record AddConnectorCommand(
    string SemanticId,
    string Source,
    string Target,
    ConnectorStyle? Style,
    IReadOnlyList<PixelPoint>? Waypoints = null);
```

- [x] **Step 3: 实现 connector 创建和胶合**

Required sequence:

1. Resolve source and target immediately before mutation.
2. Create a 1D shape with `Page.DrawLine` or the packaged connector master.
3. Glue `BeginX` to source `PinX` or an explicit connection point.
4. Glue `EndX` to target `PinX` or an explicit connection point.
5. Apply line/arrow style through universal cells.
6. Register semantic ID.
7. Enumerate `Page.Connects` and verify both endpoints.
8. If verification fails, delete the newly created connector within the same undo scope and return `CONNECTOR_NOT_GLUED`.

- [x] **Step 4: 写移动节点仍保持连接的 integration test**

Extend `VisioFixture` with:

```csharp
public static VisioFixture WithTwoShapes(string firstId, string secondId);
public void AddConnector(string semanticId, string source, string target);
public PixelPoint ReadConnectorEnd(string semanticId);
public void Move(string semanticId, PixelBox bounds);
public int CountConnections(string semanticId);
```

```csharp
[Trait("Category", "VisioIntegration")]
[Fact]
public void Glued_connector_follows_moved_target()
{
    using var fixture = VisioFixture.WithTwoShapes("source", "target");
    fixture.AddConnector("flow", "source", "target");
    var before = fixture.ReadConnectorEnd("flow");
    fixture.Move("target", new PixelBox(700, 300, 200, 100));
    var after = fixture.ReadConnectorEnd("flow");
    Assert.NotEqual(before, after);
    Assert.Equal(2, fixture.CountConnections("flow"));
}
```

- [x] **Step 5: 运行 connector tests**

```powershell
dotnet test VisioBridge.sln -c Release --filter ConnectorServiceTests
dotnet test VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj -c Release --filter ConnectorIntegrationTests
```

Expected: unit 和 integration tests PASS。

- [x] **Step 6: 提交连接线功能**

```powershell
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: add glued Visio connectors"
```

---

### Task 10: 实现 inspect、update、delete、clear 和显式撤销作用域

**Files:**
- Create: `bridge/VisioBridge.Core/Inspection/PageInspector.cs`
- Create: `bridge/VisioBridge.Core/Shapes/ShapeEditor.cs`
- Create: `bridge/VisioBridge.Core/Session/UndoScope.cs`
- Create: `bridge/VisioBridge.Contracts/Inspection.cs`
- Create: `bridge/VisioBridge.Tests/PageInspectorTests.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/FakePageFixture.cs`
- Create: `bridge/VisioBridge.IntegrationTests/EditingIntegrationTests.cs`
- Modify: `bridge/VisioBridge.Core/Commands/CommandRouter.cs`

- [x] **Step 1: 写“只修改显式字段”测试**

```csharp
[Fact]
public void Update_label_does_not_change_geometry_or_style()
{
    var fixture = FakePageFixture.WithShape("encoder", label: "Old", x: 100, fill: "#DAE8FC");
    fixture.Editor.Update(new UpdateShapeCommand("encoder", Label: "New"));
    var shape = fixture.Inspector.Resolve("encoder");
    Assert.Equal("New", shape.Label);
    Assert.Equal(100, shape.Bounds.X);
    Assert.Equal("#DAE8FC", shape.Style.FillColor);
}
```

Create `FakePageFixture.cs` with `WithShape`, `Editor`, `Inspector`, geometry and style storage. It must update only fields present in `UpdateShapeCommand`; no COM calls are allowed in this fake.

- [x] **Step 2: 定义 inspection DTO**

```csharp
public sealed record ShapeSnapshot(
    string SemanticId,
    int ShapeId,
    string NameU,
    string Type,
    string Label,
    PixelBox Bounds,
    double RotationDeg,
    string? MasterNameU,
    IReadOnlyList<string> Layers,
    IReadOnlyList<string> ConnectedTo);
```

- [x] **Step 3: 实现 PageInspector**

Inspect top-level and grouped shapes recursively; return page-space bounds; cap results at requested `max_shapes`; return `truncated`; include page count, shape count, connector count and dirty state.

- [x] **Step 4: 实现 UpdateShapeCommand**

Define the command exactly:

```csharp
public sealed record UpdateShapeCommand(
    string SemanticId,
    string? Label = null,
    double? X = null,
    double? Y = null,
    double? Width = null,
    double? Height = null,
    ShapeStyle? Style = null);
```

Every nullable field means “leave unchanged.” Resolve by semantic ID immediately before update, clone only requested geometry fields, and use `StyleMapper` for requested style fields.

- [x] **Step 5: 实现显式 delete connector policy**

DTO:

```csharp
public sealed record DeleteShapeCommand(string SemanticId, string ConnectorPolicy);
```

Accept only `keep` or `delete`. Before deleting, inspect affected connectors; delete them only when policy is exactly `delete`.

- [x] **Step 6: 实现 clear 和 UndoScope**

`clear` requires `confirm=true`. `UndoScope` wraps `Application.BeginUndoScope(name)` and always balances with `EndUndoScope(scopeId, commit)` in `Dispose`/explicit completion.

- [x] **Step 7: 写用户手动移动后更新文字的 integration test**

Test sequence:

```text
add shape -> direct COM move to new location -> inspect -> update label only -> inspect
```

Expected: new location preserved, label changed, one undo step reverses label update.

- [x] **Step 8: 运行 tests**

```powershell
dotnet test VisioBridge.sln -c Release --filter "PageInspectorTests|EditingIntegrationTests"
```

Expected: PASS。

- [x] **Step 9: 提交检查与编辑功能**

```powershell
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: inspect and edit live Visio shapes"
```

---

### Task 11: 实现 draw sequence、fit 和阶段性 PNG 预览

**Files:**
- Create: `bridge/VisioBridge.Core/Export/PreviewExporter.cs`
- Create: `bridge/VisioBridge.Core/Abstractions/IPreviewExportAdapter.cs`
- Create: `bridge/VisioBridge.Core/Commands/SequenceExecutor.cs`
- Create: `bridge/VisioBridge.Tests/SequenceExecutorTests.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/SequenceFixture.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/FakeExportAdapter.cs`
- Create: `bridge/VisioBridge.IntegrationTests/PreviewIntegrationTests.cs`
- Modify: `bridge/VisioBridge.IntegrationTests/VisioFixture.cs`
- Modify: `bridge/VisioBridge.Core/Commands/CommandRouter.cs`
- Modify: `plugins/visio-scientific-illustrator/scripts/live-server.mjs`

- [x] **Step 1: 写 sequence 中途停止测试**

```csharp
[Fact]
public void Sequence_stops_at_first_failure_and_reports_applied_count()
{
    var executor = SequenceFixture.WithResults(success: true, success: false, success: true);
    var result = executor.Execute(SequenceFixture.ThreeOperations());
    Assert.Equal(1, result.AppliedCount);
    Assert.Equal(1, result.FailedIndex);
    Assert.Equal(2, executor.Calls.Count);
}
```

Create `SequenceFixture.cs` with deterministic queued success/failure responses, a `Calls` collection, and `ThreeOperations()` returning three concrete wait/update operations. Create `FakeExportAdapter.cs` with `CurrentSize`, `ThrowOnExport`, settings snapshot/restore and an in-memory export call counter.

Define the production/test seam in `IPreviewExportAdapter.cs`:

```csharp
public sealed record RasterSettings(int WidthPx, int HeightPx, int Resolution, int Quality);
public sealed record PreviewResult(string AbsolutePath, int Width, int Height, long Bytes);

public interface IPreviewExportAdapter
{
    RasterSettings ReadSettings();
    void ApplyPreviewSettings(int widthPx);
    void ExportPage(string absolutePngPath);
    void RestoreSettings(RasterSettings settings);
}
```

- [x] **Step 2: 实现 SequenceExecutor**

Accepted operation types:

```text
shape, image, connector, update, delete, fit, wait
```

Each non-wait operation invokes the same command handler used by single tools. Stop on first error; return results, applied count, failed index and error. Do not continue after failure.

- [x] **Step 3: 在 Node live-server 执行可见延时**

Node receives sequence operations and sends one Bridge command per operation, awaiting each result, then waits `step_delay_ms`. This keeps the UI pacing outside COM and prevents long COM calls. Enforce 100–10000 ms and a maximum of 500 operations.

- [x] **Step 4: 写预览设置恢复测试接口**

Define an adapter test that records settings before/after a forced export exception:

```csharp
[Fact]
public void Preview_restores_raster_settings_when_export_fails()
{
    var adapter = FakeExportAdapter.ThrowingWithSettings(width: 800, height: 600);
    Assert.Throws<BridgeCommandException>(() => new PreviewExporter(adapter).Export(2000));
    Assert.Equal((800, 600), adapter.CurrentSize);
}
```

- [x] **Step 5: 实现 PreviewExporter**

Required behavior:

1. Snapshot raster resolution, size, quality, color and background settings.
2. Create a unique session-owned temp directory.
3. Configure width 2000 px or requested width.
4. Call `Page.Export(tempPng)`.
5. Validate PNG signature, IHDR dimensions, IEND and non-zero decoded dimensions.
6. Restore settings in `finally`.
7. Return temp path, dimensions and byte count.

- [x] **Step 6: 实现 fit**

Accept `page` or `width`; set `Application.ActiveWindow.ViewFit` to `visFitPage` or `visFitWidth`. Reject non-drawing windows with `UNSUPPORTED_OPERATION`.

- [x] **Step 7: 写 preview integration test**

Extend `VisioFixture` with:

```csharp
public static VisioFixture WithOneShape();
public RasterSettings ReadRasterSettings();
public PreviewResult ExportPreview(int widthPx);
```

```csharp
[Trait("Category", "VisioIntegration")]
[Fact]
public void Preview_exports_decodable_2000_pixel_png_and_restores_settings()
{
    using var fixture = VisioFixture.WithOneShape();
    var before = fixture.ReadRasterSettings();
    var preview = fixture.ExportPreview(2000);
    Assert.Equal(2000, preview.Width);
    Assert.True(preview.Bytes > 1000);
    Assert.Equal(before, fixture.ReadRasterSettings());
}
```

- [x] **Step 8: Node 将临时 PNG 返回为 MCP image 后清理**

`live-server.mjs` reads the returned absolute temp path, verifies it belongs to the bridge session temp directory, encodes base64, then deletes only that file. It returns `image/png` content plus structured status.

- [x] **Step 9: 运行 tests 并提交**

```powershell
npm run test:node
dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.sln -c Release --filter "SequenceExecutorTests|PreviewIntegrationTests"
git add plugins/visio-scientific-illustrator
git commit -m "feat: add paced drawing and Visio previews"
```

---

### Task 12: 实现安全 VSDX 保存、备份、验证和恢复

**Files:**
- Create: `bridge/VisioBridge.Core/Files/SafeFileWriter.cs`
- Create: `bridge/VisioBridge.Core/Files/VisioFileService.cs`
- Create: `bridge/VisioBridge.Core/Abstractions/IDocumentSaver.cs`
- Create: `bridge/VisioBridge.Core/Abstractions/IDocumentValidator.cs`
- Create: `bridge/VisioBridge.Tests/SafeFileWriterTests.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/TestDirectory.cs`
- Create: `bridge/VisioBridge.Tests/Fakes/ThrowingDocumentSaver.cs`
- Create: `bridge/VisioBridge.IntegrationTests/SaveIntegrationTests.cs`
- Modify: `bridge/VisioBridge.Core/Commands/CommandRouter.cs`

- [x] **Step 1: 写“覆盖失败保留旧文件”测试**

```csharp
[Fact]
public void Failed_overwrite_restores_original_file()
{
    using var temp = TestDirectory.Create();
    var target = temp.WriteFile("figure.vsdx", "original");
    var writer = new SafeFileWriter(new ThrowingDocumentSaver(), new PassThroughDocumentValidator());
    Assert.Throws<BridgeCommandException>(() => writer.Save(target, overwrite: true));
    Assert.Equal("original", File.ReadAllText(target));
}
```

Create the two test helpers used above:

```csharp
internal sealed class TestDirectory : IDisposable
{
    public static TestDirectory Create();
    public string WriteFile(string name, string contents);
    public void Dispose(); // 只删除本实例创建的唯一临时目录
}

internal sealed class ThrowingDocumentSaver : IDocumentSaver
{
    public void Save(string path) => throw new IOException("injected save failure");
}

internal sealed class PassThroughDocumentValidator : IDocumentValidator
{
    public void Validate(string path) { }
}
```

Define `IDocumentSaver.cs`:

```csharp
public interface IDocumentSaver
{
    void Save(string absoluteVsDxPath);
}

public interface IDocumentValidator
{
    void Validate(string absoluteVsDxPath);
}
```

- [x] **Step 2: 运行测试确认失败**

```powershell
dotnet test VisioBridge.sln -c Release --filter SafeFileWriterTests
```

Expected: FAIL。

- [x] **Step 3: 实现输出路径策略**

`SafeFileWriter` must:

- require absolute `.vsdx` paths;
- reject `.vsdm`, relative paths and directory paths;
- default overwrite false;
- if target exists and overwrite false, throw `OUTPUT_EXISTS`;
- if overwrite true, copy target to `.<filename>.<session>.backup` in the same directory;
- invoke the Visio saver;
- invoke a validator;
- delete backup only after validation passes;
- restore backup after any failure and include `restored=true/false` in error details.

- [x] **Step 4: 实现 Visio save modes**

Rules:

```text
unsaved document -> Document.SaveAsEx(path, 0)
same current FullName -> Document.Save()
different path -> Document.SaveAsEx(path, 0)
```

Never set `Document.Saved=true` to suppress prompts except inside the explicit `discard` branch of close/shutdown.

- [x] **Step 5: 实现独立只读验证入口**

File mode opens the saved file read-only/hidden, verifies:

```text
page count > 0
all page names and IDs enumerable
all Shapes enumerable
User.CodexId values unique per document
connectors have valid Connect records
page width and height > 0
```

Close the validation document without saving.

- [x] **Step 6: 写真实 overwrite failure integration test**

Use an invalid destination or injected validator failure after Visio save; assert the original SHA-256 remains unchanged and a structured `VALIDATION_FAILED` is returned.

- [x] **Step 7: 运行保存 tests**

```powershell
dotnet test VisioBridge.sln -c Release --filter "SafeFileWriterTests|SaveIntegrationTests"
```

Expected: PASS；失败场景不丢失旧文件。

- [x] **Step 8: 提交安全保存**

```powershell
git add plugins/visio-scientific-illustrator/bridge
git commit -m "feat: save and validate Visio documents safely"
```

---

### Task 13: 实现 file mode 的 validate、inspect 和 PNG/SVG/PDF 导出

**Files:**
- Modify: `bridge/VisioBridge.Host/Program.cs`
- Modify: `bridge/VisioBridge.Core/Files/VisioFileService.cs`
- Create: `bridge/VisioBridge.Core/Files/FileExportPolicy.cs`
- Create: `bridge/VisioBridge.Tests/FileExportPolicyTests.cs`
- Create: `bridge/VisioBridge.IntegrationTests/FileModeIntegrationTests.cs`
- Modify: `plugins/visio-scientific-illustrator/scripts/file-server.mjs`
- Modify: `bridge/VisioBridge.Core/BridgeErrorCodes.cs`
- Modify: `plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs`

- [x] **Step 1: 让 Host 解析显式 mode**

Accepted CLI:

```text
VisioBridge.exe --mode live
VisioBridge.exe --mode file
```

Any missing/unknown mode exits non-zero and writes a JSON-free diagnostic to stderr.

- [x] **Step 2: 写导出路由测试**

```csharp
[Theory]
[InlineData("png", "Page.Export")]
[InlineData("svg", "Page.Export")]
[InlineData("pdf", "Document.ExportAsFixedFormat")]
public void Export_format_uses_expected_Visio_API(string format, string expected)
{
    Assert.Equal(expected, FileExportPolicy.SelectApi(format));
}
```

- [x] **Step 3: 实现三种导出**

First implement the tested routing helper:

```csharp
public static string SelectApi(string format) => format switch
{
    "png" or "svg" => "Page.Export",
    "pdf" => "Document.ExportAsFixedFormat",
    _ => throw new BridgeCommandException(
        BridgeErrorCodes.UnsupportedOperation,
        $"不支持导出格式: {format}"),
};
```

- PNG/SVG: `Page.Export(tempPath)`.
- PDF: `Document.ExportAsFixedFormat(visFixedFormatPDF, tempPath, visDocExIntentPrint, requestedRange)`.
- Require explicit `page_scope="current"` or `page_scope="all"` for PDF.
- Validate output signature and size before replacing target.
- Use same-directory temp output and atomic replace; never delete an existing target after failure.

- [x] **Step 4: 实现 file inspect output parity**

Return the same `ShapeSnapshot` structure as live inspect, plus file path, Visio version, read-only flag, pages and validation warnings.

- [x] **Step 5: 定义 file MCP schemas**

Tools:

```text
visio_validate(input_path)
visio_inspect_file(input_path, max_shapes=500)
visio_export(input_path, format, output_path, page_scope, overwrite=false, width=2000)
```

All schemas set `additionalProperties=false`.

- [x] **Step 6: 写 file mode integration test**

Sequence:

```text
create fixture VSDX -> validate -> inspect -> export PNG -> export SVG -> export PDF -> verify signatures
```

Expected: source VSDX hash is unchanged; outputs exist and are non-empty.

- [x] **Step 7: 运行 Node 与 C# tests**

```powershell
npm run test:node
dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.sln -c Release --filter "FileExportPolicyTests|FileModeIntegrationTests"
```

- [x] **Step 8: 提交 file mode**

```powershell
git add plugins/visio-scientific-illustrator
git commit -m "feat: validate and export saved Visio files"
```

---

### Task 14: 完成所有 MCP 工具 schema、错误映射和服务器端参数校验

**Files:**
- Modify: `plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/mcp-common.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/live-server.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/file-server.mjs`
- Create: `tests/node/tool-schemas.test.mjs`
- Create: `tests/node/server-routing.test.mjs`

- [x] **Step 1: 写工具清单完整性测试**

```js
test("live tool names are stable and draw.io-free", () => {
  assert.deepEqual(liveTools.map((tool) => tool.name), [
    "visio_live_launch",
    "visio_live_status",
    "visio_live_clear",
    "visio_live_add_shape",
    "visio_live_add_image",
    "visio_live_add_connector",
    "visio_live_update_shape",
    "visio_live_delete_shape",
    "visio_live_draw_sequence",
    "visio_live_inspect",
    "visio_live_fit",
    "visio_live_preview",
    "visio_live_save",
    "visio_live_close_document",
    "visio_live_shutdown",
  ]);
  assert.equal(JSON.stringify(liveTools).includes("drawio"), false);
});
```

- [x] **Step 2: 为每个工具写完整 JSON Schema**

Schemas must encode:

- absolute path descriptions;
- enums for mode, shape, connector policy, fit and export format;
- finite numeric bounds;
- `step_delay_ms` 100–10000;
- sequence maxItems 500;
- inspect max 1–5000;
- overwrite default false;
- `confirm=true` requirements for clear/shutdown;
- explicit `dirty_action=save|discard|cancel` for shutdown and `action=save|discard|cancel` for close document;
- `additionalProperties=false` at every object level.

- [x] **Step 3: 在服务端实际执行参数校验**

Do not rely on the MCP client honoring schemas. Add a validator that returns `INVALID_ARGUMENT` with field path. Tests must send unknown fields, missing required fields and wrong enum values directly over JSON-RPC.

- [x] **Step 4: 建立 Bridge 错误到 MCP 错误映射**

使用 Task 3 已冻结的 `BridgeErrorCodes`，不得在 Node 层重新定义另一套名称。为每个稳定错误码写表驱动测试，逐项验证 Bridge 响应中的 `code`、`message`、`retryable`、`details` 被原样映射到 MCP `isError=true` 的结构化内容，并补充 `tool` 字段。若收到未知 Bridge 错误码，才映射为 `INTERNAL_ERROR`，同时把原始错误码放入 `details.bridge_code`，避免信息丢失。

Preserve:

```text
code, message, retryable, details, tool
```

`VISIO_BUSY` remains retryable; `SHAPE_NOT_FOUND`, `MASTER_NOT_FOUND` and `OUTPUT_EXISTS` are not retryable. Do not catch and convert every error to generic `INTERNAL_ERROR`.

- [x] **Step 5: 验证 mutating tools 走 SerialQueue**

Routing test fires two fake `add_shape` calls where the first delays; assert Bridge call order remains first then second. Status can share the queue to keep COM observation consistent.

- [x] **Step 6: 运行 Node tests**

```powershell
node --test tests/node/*.test.mjs
```

Expected: all PASS；不存在 draw.io 工具名。

- [x] **Step 7: 提交完整 MCP API**

```powershell
git add plugins/visio-scientific-illustrator/scripts tests/node
git commit -m "feat: finalize Visio MCP tool contracts"
```

---

### Task 15: 接入复杂 Master stencil、分组与容器

**Files:**
- Create: `plugins/visio-scientific-illustrator/assets/visio-scientific-masters.vssx`
- Create: `bridge/VisioBridge.Core/Shapes/MasterCatalog.cs`
- Create: `bridge/VisioBridge.Tests/MasterCatalogTests.cs`
- Create: `bridge/VisioBridge.IntegrationTests/MasterIntegrationTests.cs`
- Modify: `bridge/VisioBridge.Core/Shapes/ShapeFactory.cs`
- Modify: `plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs`
- Modify: `plugins/visio-scientific-illustrator/scripts/live-server.mjs`
- Modify: `tests/node/tool-schemas.test.mjs`

- [x] **Step 1: 定义固定通用 Master 清单**

Create `MasterCatalog.cs` mapping only:

```csharp
private static readonly IReadOnlyDictionary<string, string> Names =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rounded_rectangle"] = "Codex Rounded Rectangle",
        ["diamond"] = "Codex Diamond",
        ["cylinder"] = "Codex Cylinder",
        ["hexagon"] = "Codex Hexagon",
        ["triangle"] = "Codex Triangle",
        ["parallelogram"] = "Codex Parallelogram",
        ["cloud"] = "Codex Cloud",
        ["container"] = "Codex Container",
    };
```

- [x] **Step 2: 创建 stencil 并验证 Master NameU**

Create the `.vssx` manually in Visio developer mode or with a one-time repository build utility, then add an integration test that opens it hidden and asserts every expected `Masters.ItemU(name)` exists. Do not substitute localized built-in stencil names.

- [x] **Step 3: 实现 Master Drop**

Open the bundled stencil hidden once per owned session, resolve by exact NameU, call `Page.Drop(master, pinX, pinY)`, then set Width/Height and semantic ID. Missing Master returns `MASTER_NOT_FOUND`.

- [x] **Step 4: 实现显式 group/container operations**

Add commands:

```text
group_shapes(group_semantic_id, member_semantic_ids[])
ungroup_shape(group_semantic_id)
add_to_container(container_semantic_id, member_semantic_ids[])
```

Reject duplicate members and cross-page members. Inspector returns page-space bounds for subshapes.

Expose matching MCP tools only in this task:

```text
visio_live_group_shapes
visio_live_ungroup_shape
visio_live_add_to_container
```

Add complete schemas with `additionalProperties=false`, route them through `SerialQueue`, and extend the exact tool-name test after the Bridge commands pass.

- [x] **Step 5: 写 Master integration test**

Drop every Master, save/reopen VSDX, inspect semantic IDs and verify each Shape remains independently editable.

- [x] **Step 6: 运行测试并提交**

```powershell
dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.sln -c Release --filter "MasterCatalogTests|MasterIntegrationTests"
git add plugins/visio-scientific-illustrator/assets plugins/visio-scientific-illustrator/bridge
git commit -m "feat: add Visio scientific shape masters"
```

---

### Task 16: 全量重写 Visio Skill、插件界面和隐私边界

**Files:**
- Delete: `plugins/visio-scientific-illustrator/skills/recreate-scientific-figure-in-drawio/`
- Create: `plugins/visio-scientific-illustrator/skills/recreate-scientific-figure-in-visio/SKILL.md`
- Create: `plugins/visio-scientific-illustrator/skills/recreate-scientific-figure-in-visio/agents/openai.yaml`
- Modify: `plugins/visio-scientific-illustrator/.codex-plugin/plugin.json`
- Modify: `PRIVACY.md`
- Create: `tests/node/skill-content.test.mjs`

- [x] **Step 1: 写 Skill 禁用词和必备词测试**

```js
test("Visio skill contains required boundaries and no active draw.io terminology", async () => {
  const skill = await fs.readFile(skillPath, "utf8");
  for (const required of [
    "Visio COM automation",
    "User.CodexId",
    "CellsU",
    "FormulaU",
    "glued 1D connectors",
    "visio_live_preview",
    "visio_live_save",
  ]) assert.match(skill, new RegExp(required.replaceAll(".", "\\.")));
  for (const forbidden of ["CDP", "mxCell", "mxCodec", "graph_ready", "drawio_live_"]) {
    assert.equal(skill.includes(forbidden), false, forbidden);
  }
});
```

- [x] **Step 2: 运行测试确认失败**

```powershell
node --test tests/node/skill-content.test.mjs
```

Expected: FAIL，因为旧 Skill 仍存在。

- [x] **Step 3: 写完整 Visio SKILL.md**

Use the exact approved content from design spec section 14.5, preserving these hard boundaries:

```text
plugin-owned Visio instance only
no OS mouse/keyboard automation
no VSDX-first drawing
no completed raster/SVG pretending to be editable
no unrelated user Visio attach/close
no missing-master fallback
inspect after possible manual edits
save only after visible drawing exists
```

- [x] **Step 4: 更新 agent UI metadata**

`openai.yaml`:

```yaml
interface:
  display_name: "Microsoft Visio Scientific Figure"
  short_description: "Recreate scientific figures as editable live Visio diagrams"
  default_prompt: "Recreate this reference figure live in Microsoft Visio, preserve editable shapes and glued connectors, save VSDX, and export a review PNG."
```

- [x] **Step 5: 更新 plugin.json 完整界面元数据**

Require:

```json
{
  "name": "visio-scientific-illustrator",
  "version": "0.1.0",
  "interface": {
    "displayName": "Visio Scientific Illustrator",
    "shortDescription": "Draw editable scientific figures live inside Microsoft Visio.",
    "capabilities": ["Interactive", "Write"],
    "defaultPrompt": [
      "参考这张图，在可见的 Microsoft Visio 页面中逐步重绘可编辑科研插图。",
      "Recreate this reference figure live in Microsoft Visio, step by step."
    ]
  }
}
```

- [x] **Step 6: 更新 PRIVACY.md**

State:

```text
No network listener or telemetry.
Node and Bridge communicate over child-process stdio.
Plugin owns a dedicated Visio process.
Reference files and previews remain local unless the user separately uploads them.
Session temp files are deleted only from plugin-owned temp directories.
```

- [x] **Step 7: 运行 Skill/metadata tests 并提交**

```powershell
node --test tests/node/skill-content.test.mjs
node scripts/validate-repo.mjs
git add plugins/visio-scientific-illustrator PRIVACY.md tests/node
git commit -m "docs: replace draw.io workflow with Visio skill"
```

---

### Task 17: 完成 COM busy、生命周期、构建、安装、CI 与文档

**Files:**
- Create: `bridge/VisioBridge.Core/Interop/OleMessageFilter.cs`
- Create: `bridge/VisioBridge.Core/Interop/ComExceptionTranslator.cs`
- Create: `bridge/VisioBridge.Tests/OleMessageFilterTests.cs`
- Create: `bridge/VisioBridge.Tests/ComExceptionTranslatorTests.cs`
- Create: `scripts/build-bridge.ps1`
- Modify: `install.ps1`
- Delete: `install.sh`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `.github/workflows/ci.yml`
- Modify: `scripts/smoke-test.mjs`

- [x] **Step 1: 写 COM retry policy 测试**

```csharp
[Theory]
[InlineData(0, 250)]
[InlineData(9_500, 250)]
[InlineData(10_000, -1)]
public void Retry_later_is_bounded(int elapsedMs, int expectedDelay)
{
    var policy = new OleRetryPolicy(maxElapsedMs: 10_000, delayMs: 250);
    Assert.Equal(expectedDelay, policy.GetRetryDelay(elapsedMs, OleRejectType.RetryLater));
}

[Fact]
public void Rejected_call_is_not_silently_retried()
{
    var policy = new OleRetryPolicy(10_000, 250);
    Assert.Equal(-1, policy.GetRetryDelay(0, OleRejectType.Rejected));
}
```

- [x] **Step 2: 实现并注册 IMessageFilter**

Register on the STA thread before Visio creation; unregister during dispatcher shutdown. Map exhausted retry to `VISIO_BUSY` with `retryable=true`. Do not retry arbitrary COM exceptions.

Define the tested policy types in `OleMessageFilter.cs`:

```csharp
public enum OleRejectType { Rejected, RetryLater }

public sealed class OleRetryPolicy
{
    public OleRetryPolicy(int maxElapsedMs, int delayMs)
    {
        if (maxElapsedMs <= 0 || delayMs <= 0) throw new ArgumentOutOfRangeException();
        MaxElapsedMs = maxElapsedMs;
        DelayMs = delayMs;
    }

    public int MaxElapsedMs { get; }
    public int DelayMs { get; }

    public int GetRetryDelay(int elapsedMs, OleRejectType rejectType) =>
        rejectType == OleRejectType.RetryLater && elapsedMs < MaxElapsedMs ? DelayMs : -1;
}
```

`IMessageFilter.RetryRejectedCall` must delegate to this policy; `-1` cancels the COM call.

Implement the COM contract and registration explicitly in the same file:

```csharp
[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(int callType, IntPtr callerTask, int tickCount, IntPtr interfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(IntPtr calleeTask, int tickCount, int rejectType);

    [PreserveSig]
    int MessagePending(IntPtr calleeTask, int tickCount, int pendingType);
}

internal sealed class OleMessageFilter : IOleMessageFilter, IDisposable
{
    private const int ServerCallIsHandled = 0;
    private const int ServerCallRejected = 1;
    private const int ServerCallRetryLater = 2;
    private const int PendingMessageWaitDefaultProcess = 2;

    private readonly OleRetryPolicy _policy;
    private IOleMessageFilter? _previous;
    private bool _registered;

    private OleMessageFilter(OleRetryPolicy policy) => _policy = policy;

    public static OleMessageFilter Register(OleRetryPolicy policy)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("OLE message filter must be registered on the Visio STA thread.");

        var filter = new OleMessageFilter(policy);
        Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(filter, out filter._previous));
        filter._registered = true;
        return filter;
    }

    public int HandleInComingCall(int callType, IntPtr callerTask, int tickCount, IntPtr interfaceInfo) =>
        ServerCallIsHandled;

    public int RetryRejectedCall(IntPtr calleeTask, int tickCount, int rejectType) =>
        rejectType switch
        {
            ServerCallRetryLater => _policy.GetRetryDelay(tickCount, OleRejectType.RetryLater),
            ServerCallRejected => _policy.GetRetryDelay(tickCount, OleRejectType.Rejected),
            _ => -1,
        };

    public int MessagePending(IntPtr calleeTask, int tickCount, int pendingType) =>
        PendingMessageWaitDefaultProcess;

    public void Dispose()
    {
        if (!_registered) return;
        Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(_previous, out _));
        _registered = false;
        _previous = null;
    }

    [DllImport("Ole32.dll")]
    private static extern int CoRegisterMessageFilter(
        IOleMessageFilter? newFilter,
        out IOleMessageFilter? oldFilter);
}
```

Add `using System.Runtime.InteropServices;` and ensure `Register`/`Dispose` execute on the same STA thread. In `ComExceptionTranslator`, map only `RPC_E_CALL_REJECTED` (`0x80010001`) and `RPC_E_SERVERCALL_RETRYLATER` (`0x8001010A`) to `BridgeCommandException(BridgeErrorCodes.VisioBusy, ..., retryable: true)`; preserve every other COM HRESULT as `INTERNAL_ERROR` with `details.hresult`.

Add tests for: registration rejected on an MTA thread, reject type `2` returns bounded delay, reject type `1` returns `-1`, unknown reject type returns `-1`, and the two HRESULTs alone map to `VISIO_BUSY`.

- [x] **Step 3: 写 Bridge 发布脚本**

Create `scripts/build-bridge.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\bridge\VisioBridge.Host\VisioBridge.Host.csproj"
$output = Join-Path $PSScriptRoot "..\plugins\visio-scientific-illustrator\bin\win-x64"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output
if ($LASTEXITCODE -ne 0) { throw "VisioBridge publish failed." }
if (-not (Test-Path (Join-Path $output "VisioBridge.Host.exe"))) { throw "Published bridge executable is missing." }
```

Rename or configure `AssemblyName` as `VisioBridge` so final output is `VisioBridge.exe`.

Set this exact property in `VisioBridge.Host.csproj`:

```xml
<PropertyGroup>
  <AssemblyName>VisioBridge</AssemblyName>
</PropertyGroup>
```

- [x] **Step 4: 更新 install.ps1 环境检查**

Installer must check:

```powershell
$visioType = [type]::GetTypeFromProgID("Visio.Application", $false)
if ($null -eq $visioType) { throw "Microsoft Visio desktop COM automation is not registered." }
$bridge = Join-Path $InstallDir "plugins\visio-scientific-illustrator\bin\win-x64\VisioBridge.exe"
if (-not (Test-Path $bridge)) { throw "Bundled VisioBridge.exe is missing." }
```

Use plugin ID `visio-scientific-illustrator@visio-scientific-tools`.

- [x] **Step 5: 删除 install.sh 并在 README 声明 Windows-only**

README sections:

```text
What it is
Architecture
Requirements
Install
Live workflow
Tool reference
COM and process ownership
Save/export safety
Troubleshooting
Known limitations
Security and privacy
Development and tests
```

- [x] **Step 6: 更新 CI 为跨平台 Node + Windows Bridge 两个 job**

Required workflow shape:

```yaml
jobs:
  node:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 22
      - run: npm run check
      - run: npm run test:node
  bridge:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.sln -c Release --filter "Category!=VisioIntegration"
```

Because hosted CI has no Visio, COMReference resolution may require checking in an embedded Interop assembly generated on a licensed development machine. If CI cannot resolve the registered type library, stop and change the build packaging explicitly; do not switch to an unrelated NuGet wrapper automatically.

- [x] **Step 7: 扩展 smoke test**

Assert both servers:

```text
initialize succeeds
tools/list returns expected exact names
unknown tool returns isError=true
stdout contains only valid MCP JSON
process exits cleanly when stdin closes
```

- [x] **Step 8: 更新 CHANGELOG**

Add `0.1.0` entry describing Windows-only visible Visio COM bridge, editable VSDX, glued connectors, preview/export, safe overwrite and new Skill.

- [x] **Step 9: 运行非 Visio 全套检查**

```powershell
npm run check
npm run test:node
dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.sln -c Release --filter "Category!=VisioIntegration"
```

Expected: all PASS。

- [x] **Step 10: 构建 Bridge 并检查产物**

```powershell
npm run build:bridge
Get-Item plugins/visio-scientific-illustrator/bin/win-x64/VisioBridge.exe
```

Expected: self-contained x64 executable exists and returns ready/status over stdio.

- [x] **Step 11: 提交硬化、安装和文档**

```powershell
git add .github README.md CHANGELOG.md install.ps1 package.json scripts plugins/visio-scientific-illustrator
git commit -m "chore: harden and package Visio plugin"
```

---

### Task 18: 执行真实 Visio 端到端验收与发布门禁

**Files:**
- Create: `docs/verification/visio-live-acceptance.md`
- Modify: `CHANGELOG.md` only if verified behavior differs from documented behavior
- No feature code changes in this task; failures return to the owning task.

- [x] **Step 1: 确认工作树和环境**

```powershell
git status --short
node --version
dotnet --version
$type = [type]::GetTypeFromProgID("Visio.Application", $false); $type.GUID
```

Expected: clean worktree；Node 22+；.NET 8；Visio COM GUID non-null。

- [x] **Step 2: 运行全部自动测试**

```powershell
npm test
dotnet test plugins/visio-scientific-illustrator/bridge/VisioBridge.IntegrationTests/VisioBridge.IntegrationTests.csproj -c Release --filter "Category=VisioIntegration"
```

Expected: all PASS；integration tests only close their owned Visio processes。

- [x] **Step 3a: 安装本地插件**

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -InstallDir (Get-Location).Path
```

Expected: marketplace 和 plugin 安装成功。

- [ ] **Step 3b: 重启 Codex 并验证 UI 发现**

Expected: Codex 重启后出现 Visio Skill 与两个 MCP 服务。安装包、Skill 和 MCP 工具发现已通过校验；本步需要用户在当前任务结束后重启 Codex 才能完成。

- [x] **Step 4: 执行最小可见绘图验收**

In a fresh Codex task, request:

```text
在可见 Microsoft Visio 页面中逐步绘制两个圆角矩形“Input”和“Encoder”，
用一条已胶合的箭头从 Input 连接到 Encoder。每步等待 500 ms，
导出 2000 px 预览，保存为绝对路径下的 acceptance.vsdx。
```

Expected:

```text
Visio visible
three separate visible operations
editable text
glued connector follows moved Encoder
preview decodes at width 2000
VSDX opens after plugin session closes
```

- [x] **Step 5: 执行用户手工编辑协作验收**

Manually move `Encoder`, change its fill color, then ask Codex to change only its label to `Backbone`.

Expected: label changes；manual position and fill remain unchanged；Visio undo reverses only the label change。

- [x] **Step 6: 执行保存与导出故障验收**

Prepare existing `.vsdx` and `.png` files, force invalid output/validation failure, and verify SHA-256 values remain unchanged.

Expected: MCP returns structured error with `restored=true` where applicable；old files remain intact。

- [x] **Step 7: 执行所有权和关闭验收**

Open a separate user-owned Visio manually, then launch and shut down the plugin session.

Expected: plugin-owned process exits；manually opened Visio remains running and untouched。

- [x] **Step 8: 记录验收结果**

Create `docs/verification/visio-live-acceptance.md` with:

```markdown
# Visio Live Acceptance

- Date: 2026-07-22
- Visio version: 16.0.20131.20154 x64
- Node tests: PASS
- Bridge unit tests: PASS
- Visio integration tests: PASS
- Live drawing: PASS
- Manual edit preservation: PASS
- Safe overwrite: PASS
- Process ownership: PASS
- Delivered files: acceptance.vsdx, acceptance.png
```

Record any failure as FAIL with the exact tool/error code; do not mark release ready while any item is unresolved.

- [x] **Step 9: 最终提交验收记录**

```powershell
git add docs/verification/visio-live-acceptance.md CHANGELOG.md
git commit -m "test: record Visio live acceptance"
git status --short
```

Expected: clean worktree and an auditable acceptance commit。

---

## 2. 完成定义

实施完成必须同时满足：

- 除需要用户重启 Codex 的 Task 18 Step 3b 外，所有 Task 1–18 checkbox 已勾选。
- Node MCP、Bridge 单元测试和真实 Visio 集成测试通过。
- Skill 中没有活动的 CDP、mxGraph、draw.io tool name 或 XML-first 约束。
- Shape 使用 `User.CodexId`，连接线真实 GlueTo。
- 用户中途编辑不会被未请求字段覆盖。
- PNG 预览恢复 Visio 导出设置。
- PNG/SVG 使用 `Page.Export`，PDF 使用 `Document.ExportAsFixedFormat`。
- VSDX 保存覆盖失败可恢复，旧文件不丢失。
- 插件只关闭 owned Visio PID。
- 发布包包含可运行的 `win-x64/VisioBridge.exe`。
- README、PRIVACY、CHANGELOG、manifest、Skill 和实际工具行为一致。
- `docs/verification/visio-live-acceptance.md` 全部为 PASS。
