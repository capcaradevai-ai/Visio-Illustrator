# Test Edition Phase 2：能力发现、参考图与图片资产实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` and `test-driven-development`. Complete Phase 1 first.

**Goal:** 让模型用一次轻量预处理获得稳定参考图坐标和区域裁剪，并让 Bridge 动态报告真实 Visio 能力；所有图片都必须具备可持久化、可重开审计的原子资产声明。

**Architecture:** Reference Manifest 和缓存由 Node 文件工具层管理；像素解码、色板、前景边界和裁剪通过 Test Bridge 的 Windows 图像服务执行，避免插件安装时下载原生 npm 依赖。Visio 能力由 C# Bridge 分 installed/session/document 三阶段探测；图片声明由合同层强制验证，再由 COM 层同时写入 User Cells 与 Shape Data。

**Tech Stack:** Node.js、SHA-256、.NET 8、System.Drawing.Common（仅 Windows）、PNG/JPEG、Visio COM、ShapeSheet User Cells、Shape Data。

**Path Key:** 下文 `TEST_PLUGIN_ROOT` 严格表示仓库相对路径 `editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test`。

---

## Task 1：定义 capability 合同和分层查询

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/CapabilityContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Capabilities/CapabilityService.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Abstractions/IVisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/CommandRouter.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/CapabilityServiceTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/capability-contract.test.mjs`

- [ ] **Step 1：写失败测试**

公开工具固定为：

```text
visio_test_live_get_capabilities
visio_test_live_get_capability_details
```

摘要响应必须包含：

```json
{
  "edition": "test",
  "protocol_version": "0.4.0-test.1",
  "profile_sha256": "64 lowercase hex characters",
  "phase": "installed",
  "visio": {
    "installed": true,
    "version": "string or null",
    "build": "string or null",
    "architecture": "x64 or unknown"
  },
  "families": {},
  "limitations": []
}
```

测试 `profile_sha256` 必须由不含自身 hash 字段的规范 JSON 计算；同一能力对象重复查询 hash 相同。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/capability-contract.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter CapabilityServiceTests
```

Expected: FAIL，工具和服务尚不存在。

- [ ] **Step 3：实现三阶段探测**

`CapabilityService` 必须明确区分：

- `installed`：注册表/COM ProgID、Bridge 架构、内置合同能力；
- `session`：Visio 版本、build、已打开 stencil、Masters、图片导入与裁切、连接线、图层、容器、组合；
- `document`：当前页、PageSheet、页面尺寸、图层、容器、当前导出格式；
- `unavailable`：返回明确 reason，不替换为其他能力。

详情类别固定为：

```text
masters
primitives
connectors
components
images
layers
containers
shapesheet
exports
limitations
```

未知类别返回 `CAPABILITY_NOT_AVAILABLE`。

- [ ] **Step 4：实现 MCP 路由与紧凑输出**

`get_capabilities` 默认只返回摘要、计数、限制和 hash；`get_capability_details` 接受 `category` 与分页参数 `cursor`、`limit`，默认 `limit=50`，最大 `200`。不得在摘要中返回完整 Master 清单。

- [ ] **Step 5：运行测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/capability-contract.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter CapabilityServiceTests
```

Expected: PASS。

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): report dynamic visio capabilities"
```

## Task 2：实现 Reference Manifest

**Files:**

- Create: `TEST_PLUGIN_ROOT/scripts/reference-schema.mjs`
- Create: `TEST_PLUGIN_ROOT/scripts/reference-store.mjs`
- Create: `TEST_PLUGIN_ROOT/scripts/reference-image.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/file-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/ReferenceImageContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Reference/ReferenceImageService.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/FileCommandRouter.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/VisioTestBridge.Core.csproj`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/ReferenceImageServiceTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/reference-manifest.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/fixtures/reference/simple-reference.png`

- [ ] **Step 1：写失败合同测试**

公开工具：

```text
visio_test_prepare_reference
visio_test_get_reference_manifest
visio_test_get_reference_region
visio_test_extract_reference_asset
```

`prepare_reference` 输入：

```json
{
  "source_path": "absolute local PNG or JPEG path",
  "candidate_regions": [
    {"id": "left", "bounds": {"x": 0, "y": 0, "width": 400, "height": 500}},
    {"id": "right", "bounds": {"x": 400, "y": 0, "width": 400, "height": 500}}
  ]
}
```

Manifest 必须包含：

```text
reference_id
source_path
source_sha256
width_px
height_px
palette
foreground_bounds
candidate_regions
candidate_assets
coordinate_transform
manifest_sha256
```

测试必须覆盖：

- 相对路径拒绝；
- 不支持格式返回 `REFERENCE_MANIFEST_INVALID`；
- 越界区域返回 `REFERENCE_CROP_INVALID`；
-相同源文件和区域得到相同 `reference_id` 与 hash；
- manifest hash 不包含 `manifest_sha256` 自身；
-输出全部位于会话拥有目录。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/reference-manifest.test.mjs
```

Expected: FAIL，参考图模块尚不存在。

- [ ] **Step 3：实现一次预处理**

实现：

- Node 先验证绝对路径、扩展名和原始字节 SHA-256；
- Test Bridge 文件模式通过 `System.Drawing.Common` 解码 PNG/JPEG，禁止 EXIF 自动旋转造成坐标漂移，显式返回规范后的像素方向；
- 计算最多 12 色的量化主色板；
- 计算 alpha/亮度边界得到 `foreground_bounds`；
- 验证并规范化用户提供区域；
- 生成顶部左原点像素到 SceneGraph 的恒等坐标变换；
- 使用 `reference_id` 隔离会话缓存；
- manifest 写入 `<session>/references/<reference_id>/manifest.json`；
- 重复调用命中相同 manifest，不重复生成裁剪。

Bridge 私有命令固定为 `inspect_reference_image` 和 `crop_reference_image`；它们不启动 Visio、不开放为 live 工具。裁剪统一输出无元数据的 RGBA PNG。`System.Drawing.Common` 只用于 Windows Test Bridge，发布的 self-contained exe 必须携带所需程序集，安装时不运行 `npm install`。

候选区域的自动建议只能作为 `candidate_regions`，不得被当作已确认语义区域。

- [ ] **Step 4：实现按需裁剪**

`get_reference_region` 返回紧裁剪的本地路径、尺寸、bounds 和 SHA-256；默认不内联图片。`extract_reference_asset` 必须要求显式 bounds、`semantic_role`，并拒绝：

- 面积超过参考图 20%；
- 宽或高超过参考图 60%；
- 空白占比超过 70%；
-裁剪超出源图。

- [ ] **Step 5：运行测试并检查确定性**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/reference-manifest.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter ReferenceImageServiceTests
```

Expected: PASS；连续两次运行生成相同 hash。

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/scripts editions/visio-scientific-illustrator-test/tests
git commit -m "feat(test-edition): add deterministic reference manifests"
```

## Task 3：定义并强制图片资产声明

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/RasterAssetContracts.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/ShapeCommands.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/SceneContracts.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/scene-schema.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Assets/RasterDeclarationValidator.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Shapes/ShapeFactory.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/RasterDeclarationValidatorTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/raster-schema.test.mjs`

- [ ] **Step 1：写失败测试**

`visio_test_live_add_image` 和 SceneGraph image node 都必须要求：

```json
{
  "asset_id": "router-icon-1",
  "source_type": "reference_crop",
  "raster_reason": "Complex small illustration is retained as one object.",
  "source_is_tightly_cropped": true,
  "atomic_raster_unit": true,
  "contains_reconstructable_content": false,
  "decomposition_note": "Text, border and connectors were rebuilt as Visio objects.",
  "editable_scope": "object_only",
  "source_reference_sha256": "64 lowercase hex characters",
  "source_bounds": {"x": 12, "y": 20, "width": 80, "height": 60},
  "asset_sha256": "64 lowercase hex characters",
  "semantic_role": "wireless_router"
}
```

`source_type` 只允许：

```text
reference_crop
user_file
generated_external
```

字段条件：

- `reference_crop`：`source_reference_sha256` 和 `source_bounds` 必填；
- `user_file`：两者必须为 null，并记录用户文件本身的 `asset_sha256`；
- `generated_external`：两者必须为 null，并额外记录非空 `provenance_note`，说明图片由宿主在插件外生成；
- 三种来源都不允许网络 URL。

`atomic_raster_unit` 不是 `true`、`contains_reconstructable_content` 不是 `false`、理由或拆分说明为空时，返回 `RASTER_DECLARATION_INVALID`，不得创建图片 shape。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/raster-schema.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter RasterDeclarationValidatorTests
```

- [ ] **Step 3：实现严格验证**

验证顺序固定：

1. 绝对文件路径和文件存在；
2. 资产文件 SHA-256 与声明一致；
3. 枚举与布尔约束；
4. reference crop 的源 reference hash 与 bounds；
5. user/generated 文件的 provenance；
6. 面积限制；
7. 声明文本长度与控制字符。

任何失败不改变页面 revision。

- [ ] **Step 4：接入 ShapeFactory 和 SceneGraph**

`AddImageCommand` 增加 `Declaration`；`SceneImageNode` 增加相同合同。Shape 创建成功后返回 `asset_id`、`asset_sha256` 和 `declaration_persisted=false`，下一任务完成持久化后改为 `true`。

- [ ] **Step 5：运行测试并提交**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/raster-schema.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter RasterDeclarationValidatorTests
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): require atomic raster declarations"
```

## Task 4：把图片声明持久化到 Visio

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Persistence/ShapeMetadataService.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Persistence/ShapeDataNames.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Shapes/ShapeFactory.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Inspection/PageInspector.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/ShapeMetadataServiceTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.IntegrationTests/RasterPersistenceIntegrationTests.cs`

- [ ] **Step 1：写 User Cells 与 Shape Data 测试**

图片 User Cells 固定前缀：

```text
User.VsiRasterAssetId
User.VsiRasterSourceType
User.VsiRasterReason
User.VsiRasterTightlyCropped
User.VsiRasterAtomicUnit
User.VsiRasterContainsReconstructable
User.VsiRasterDecompositionNote
User.VsiRasterEditableScope
User.VsiRasterSourceReferenceSha256
User.VsiRasterSourceBoundsJson
User.VsiRasterAssetSha256
User.VsiRasterSemanticRole
```

Shape Data 行使用同名 label，但 `Prop.VsiRaster*` 存储人类可读值。长 JSON 和文本必须正确转义 ShapeSheet 字符串公式。

- [ ] **Step 2：运行失败测试**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter ShapeMetadataServiceTests
```

- [ ] **Step 3：实现双写和双读比较**

要求：

- 先写 User Cells，再写 Shape Data；
- 任一步失败时删除新建 shape 并抛出原错误；
- Inspector 返回两套数据、规范化声明和 `consistent`；
-缺失或不一致不自动修复；
- 后续 Reviewer 将其转为 hard finding。

- [ ] **Step 4：运行可见 Visio 集成测试**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter RasterPersistenceIntegrationTests
```

测试流程：启动 Test 自有可见 Visio → 添加一个 64×64 原子图片 → 保存 → 关闭 → 重开 → 比较 User Cells、Shape Data 和文件 hash → 关闭 Test 进程。

- [ ] **Step 5：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): persist raster provenance in visio"
```

## Task 5：阶段回归

- [ ] 运行：

```powershell
npm --prefix editions/visio-scientific-illustrator-test run test:node
npm --prefix editions/visio-scientific-illustrator-test run test:bridge
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter "CapabilityIntegrationTests|RasterPersistenceIntegrationTests"
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
git diff --check
```

- [ ] 扫描 UTF-8 和占位符，确认 Test 新文件不含 `�`、`TODO`、`TBD`、`PLACEHOLDER`。

- [ ] 回归没有产生文件变化时不创建空提交；若修复了回归问题，将修复文件与对应新增测试提交为 `fix(test-edition): repair reference capability regression`。
