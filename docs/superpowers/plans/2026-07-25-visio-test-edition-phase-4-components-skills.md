# Test Edition Phase 4：复合组件、五角色 Skill 与质量配置实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans`, `test-driven-development`, and `skill-creator`. Complete Phases 1–3 first.

**Goal:** 用高层、低 token 工具创建可编辑表格、图表、矩阵和网格，并把单一大 Skill 拆为总编排、Designer、Drawer、Reviewer、Corrector 五个职责清晰的 Skill。

**Architecture:** 复合组件由纯合同计划器生成独立 Visio primitives，再由一个公开工具调用原子执行并只增加一次 page revision。角色 Skill 通过状态句柄和紧凑 JSON 交接，确定性工作由工具完成，模型只处理设计歧义、主观视觉和科学语义。

**Tech Stack:** .NET 8、Visio COM、SceneGraph、Node MCP、Codex/Claude Skill Markdown。

**Path Key:** 下文 `TEST_PLUGIN_ROOT` 严格表示仓库相对路径 `editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test`。

---

## Task 1：定义通用复合组件合同

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/CompositeComponentContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/CompositeComponentPlan.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/CompositeComponentExecutor.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/ComponentMetadataService.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/CompositeComponentContractTests.cs`

- [ ] **Step 1：写失败测试**

所有组件共同字段：

```json
{
  "semantic_id": "accuracy-chart",
  "region_id": "results",
  "bounds": {"x": 420, "y": 60, "width": 320, "height": 220},
  "style": {},
  "expected_revision": 12
}
```

父对象必须保存：

```text
User.VsiComponentType
User.VsiComponentSchemaVersion
User.VsiComponentDataSha256
User.VsiComponentLayoutVersion
User.VsiComponentChildIdsJson
```

子 ID 采用层级点号格式并保持稳定，例如：

```text
accuracy-chart.axis.x
accuracy-chart.series.method-a.point.1
accuracy-chart.legend.method-a
```

- [ ] **Step 2：实现 plan/execute 分离**

计划器不访问 COM，输出有序 primitive 列表、父子关系、预期 bounds 和连接点。执行器：

1. 验证整个合同；
2. 在 Visio undo scope 内创建所有 primitives；
3. 写父子元数据；
4. group 组件；
5. 失败时撤销整次调用；
6. 成功只增加一次 page revision。

不允许部分组件留在页面上。

- [ ] **Step 3：运行测试**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter CompositeComponentContractTests
```

- [ ] **Step 4：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add atomic composite component plans"
```

## Task 2：实现可编辑表格

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/TableContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/TableComponentPlanner.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/CommandRouter.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/TableComponentPlannerTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.IntegrationTests/TableComponentIntegrationTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/table-tools.test.mjs`

- [ ] **Step 1：写失败测试**

工具：

```text
visio_test_live_add_table
visio_test_live_update_table_cell
visio_test_live_update_table_layout
```

首版合同支持：

- 1–50 行、1–20 列；
- 每格独立文本和背景；
- 可选 header row/header column；
- 行高、列宽、边框、对齐；
- 合并单元格不在 `0.4.0-test.1` 支持范围，显式返回 `CAPABILITY_NOT_AVAILABLE`。

- [ ] **Step 2：实现独立 shape 表格**

每个 cell 使用一个可编辑矩形/文本对象，不使用图片、OLE 表格或嵌入 Excel。ID：

```text
<table>.cell.r<row>.c<column>
<table>.border.top
<table>.border.right
<table>.border.bottom
<table>.border.left
```

更新单元格不得重建整个表格；更新 layout 可以移动/缩放现有子 shape，但必须保持子 ID。

- [ ] **Step 3：运行单元、Node 和集成测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/table-tools.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter TableComponentPlannerTests
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter TableComponentIntegrationTests
```

- [ ] **Step 4：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add editable visio tables"
```

## Task 3：实现可编辑统计图

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/ChartContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/ChartComponentPlanner.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/CommandRouter.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/ChartComponentPlannerTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.IntegrationTests/ChartComponentIntegrationTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/chart-tools.test.mjs`

- [ ] **Step 1：写失败测试**

工具：

```text
visio_test_live_add_chart
visio_test_live_update_chart
```

图表类型：

```text
bar
line
scatter
error_bar
```

合同必须显式携带 series、x/y domain、axis labels、tick policy、legend policy、颜色和误差值。NaN、Infinity、长度不一致或 domain 为空返回 `COMPONENT_CONTRACT_INVALID`。

- [ ] **Step 2：实现图元化图表**

独立可编辑对象：

- x/y axis；
- ticks 与 tick labels；
- axis titles；
- 每个 bar/point/line segment/error bar；
- legend swatch 与 text；
- chart title。

不使用 Visio chart OLE、不嵌入 Excel、不把图表渲染为图片。折线和误差线使用原生 line/path，数据点使用独立 shapes。

- [ ] **Step 3：实现确定性布局**

相同输入、bounds 和 style 必须产生相同 primitive bounds。轴域默认只在调用者省略且数据有明确 min/max 时计算；常数序列通过确定规则扩展 5%，不使用模型猜测。更新保持可复用子 ID。

- [ ] **Step 4：运行测试并提交**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/chart-tools.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter ChartComponentPlannerTests
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter ChartComponentIntegrationTests
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add editable statistical charts"
```

## Task 4：实现矩阵、热力矩阵和规则网格

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/MatrixGridContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/MatrixComponentPlanner.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Components/GridComponentPlanner.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/CommandRouter.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/MatrixGridPlannerTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/matrix-grid-tools.test.mjs`

- [ ] **Step 1：写失败测试**

工具：

```text
visio_test_live_add_matrix
visio_test_live_update_matrix_cell
visio_test_live_add_grid
visio_test_live_update_grid
```

矩阵上限 `50 × 50`，网格上限 2500 cells。超过上限返回 `COMPONENT_CONTRACT_INVALID`，不得通过拆批绕过。

- [ ] **Step 2：实现布局**

数值矩阵每格独立 shape/text；热力矩阵颜色由显式 scale 确定；规则网格允许 cell labels、行列标签和高亮。更新单格不得重建全组件。

- [ ] **Step 3：运行测试并提交**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/matrix-grid-tools.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter MatrixGridPlannerTests
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add editable matrices and grids"
```

## Task 5：实现质量配置与模型升级决策

**Files:**

- Create: `TEST_PLUGIN_ROOT/scripts/quality-profiles.mjs`
- Create: `TEST_PLUGIN_ROOT/scripts/review-policy.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/quality-profiles.test.mjs`

- [ ] **Step 1：写失败测试**

固定配置：

```js
const profiles = {
  strict: {
    modelReview: "every_region",
    modelCorrector: "every_failure",
    finalModelReview: true,
  },
  adaptive: {
    modelReview: "calibration_complexity_failure",
    modelCorrector: "uncertain_or_unsafe",
    finalModelReview: true,
  },
  economy: {
    modelReview: "failure_or_low_confidence",
    modelCorrector: "uncertain_or_unsafe",
    finalModelReview: true,
  },
};
```

默认预算：

```json
{
  "quality_profile": "adaptive",
  "max_model_review_rounds_per_region": 1,
  "max_full_page_model_reviews": 1,
  "return_preview_policy": "failures_only",
  "capability_detail_policy": "on_demand",
  "auto_apply_safe_patches": true
}
```

- [ ] **Step 2：实现升级触发器**

Adaptive 必须在以下情况 `model_review_required=true`：

- 第一地区校准；
- 复杂度最高区域；
- hard finding；
- 视觉指标低于阈值；
-语义或文字置信度不足；
-连接拓扑复杂或方向不确定；
-多个图片资产；
-复杂图表/矩阵/密集网格；
-安全补丁后仍失败；
-表达方式决策置信度不足。

第一地区与最复杂区域相同只计一次。达到模型轮次上限且仍不确定时返回阻塞结果，不自动通过。

- [ ] **Step 3：实现复杂度分数**

分数只使用工具可测字段：

```text
object_count
connector_count
connector_crossing_count
text_density
composite_component_count
raster_asset_count
overlap_density
visual_metric_uncertainty
```

权重固定在 `quality-profiles.mjs`，响应返回总分和触发项，不返回完整对象清单。

- [ ] **Step 4：运行测试并提交**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/quality-profiles.test.mjs
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add cost-aware quality profiles"
```

## Task 6：拆分五角色 Skill

**Files:**

- Create: `TEST_PLUGIN_ROOT/skills/visio-test-orchestrate-scientific-figure/SKILL.md`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-design-scientific-figure/SKILL.md`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-draw-scientific-figure-live/SKILL.md`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-audit-scientific-figure/SKILL.md`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-correct-scientific-figure/SKILL.md`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-orchestrate-scientific-figure/agents/openai.yaml`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-design-scientific-figure/agents/openai.yaml`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-draw-scientific-figure-live/agents/openai.yaml`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-audit-scientific-figure/agents/openai.yaml`
- Create: `TEST_PLUGIN_ROOT/skills/visio-test-correct-scientific-figure/agents/openai.yaml`
- Create: `TEST_PLUGIN_ROOT/references/quality-contract.md`
- Create: `TEST_PLUGIN_ROOT/references/finding-taxonomy.md`
- Create: `TEST_PLUGIN_ROOT/references/component-contracts.md`
- Create: `TEST_PLUGIN_ROOT/references/raster-assets.md`
- Create: `TEST_PLUGIN_ROOT/references/tool-workflows.md`
- Create: `TEST_PLUGIN_ROOT/references/quality-profiles.md`
- Create: `editions/visio-scientific-illustrator-test/tests/node/skill-boundaries.test.mjs`

- [ ] **Step 1：写边界测试**

测试 frontmatter 名称与目录一致，并断言：

- Orchestrator 是推荐入口；
- Designer 明确“不执行绘图”；
- Drawer 明确“不批准自己的结果”；
- Reviewer 明确“不修改页面”；
- Corrector 明确“不批准自己的修正”；
-所有 Visio 工具名使用 `visio_test_` 前缀；
-不含 draw.io 的 `mxCell`、`mxGraph`、`graph model cell` 等专属术语；Visio ShapeSheet cell 和表格 cell 属于合法 Visio 术语；
-不含 PowerPoint 的 `slide`、`shape tree` 专属流程；
-图片规则引用 `raster-assets.md`；
-每个逻辑区域必须先通过 Bridge 状态机，才能继续。

- [ ] **Step 2：写 Orchestrator**

固定流程：

```text
get capabilities
prepare reference
choose profile and budget
Designer creates global plan
for each allowed region:
  Drawer draws current region
  deterministic audit
  optional model Reviewer
  safe patch or Corrector if required
  re-audit and sign pass
final whole-page model review
save, validate, export
```

Skill 必须要求工具返回的状态句柄，不在角色间复制完整 SceneGraph、capability 清单或 finding 详情。

- [ ] **Step 3：写四个角色 Skill 与 references**

每个 SKILL.md 只保留角色触发条件、职责、禁止事项、输入/输出合同和核心流程。详细规则放入六个 reference 文件，按任务需要读取，避免每次加载全部文档。

- [ ] **Step 4：运行 skill 测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/skill-boundaries.test.mjs
```

- [ ] **Step 5：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/skills editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/references editions/visio-scientific-illustrator-test/tests/node/skill-boundaries.test.mjs
git commit -m "feat(test-edition): split scientific illustration roles"
```

## Task 7：简单可见 Visio 闭环测试

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.IntegrationTests/SimpleQualityLoopIntegrationTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/fixtures/simple-quality-loop/scene.json`
- Create: `editions/visio-scientific-illustrator-test/tests/fixtures/simple-quality-loop/reference.png`
- Create: `editions/visio-scientific-illustrator-test/scripts/run-simple-quality-loop.mjs`

- [ ] **Step 1：固定测试图**

必须严格使用：

- canvas `800 × 500`；
-区域 `left`、`right`；
- 8–12 个基础对象；
- 2 条双端胶合连接线；
- `2 × 3` 表格；
-三柱 bar chart；
-一个 64×64 原子图片；
-一条故意穿过标签的连接线。

- [ ] **Step 2：执行闭环**

```text
left: draw -> audit -> pass
attempt right before left pass: QUALITY_GATE_BLOCKED
right: draw -> audit -> connector_clearance hard finding
apply move_connector_waypoint safe patch
right: re-audit -> pass
whole page audit -> save -> reopen -> validate metadata
```

- [ ] **Step 3：运行**

```powershell
node editions/visio-scientific-illustrator-test/scripts/run-simple-quality-loop.mjs --step-delay-ms 50
```

后台自动测试使用 50 ms 以节省时间；面向用户的真实绘图默认仍为 500 ms。Expected: 仅启动 Test 自有可见 Visio，生成一份临时 VSDX 和审查 JSON，所有断言通过后正常关闭。

- [ ] **Step 4：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "test(test-edition): cover a simple visible quality loop"
```

## Task 8：阶段回归

```powershell
npm --prefix editions/visio-scientific-illustrator-test test
node editions/visio-scientific-illustrator-test/scripts/run-simple-quality-loop.mjs --step-delay-ms 50
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
git diff --check
```

- [ ] 确认无整图截图、整面板截图或 OLE 图表进入测试 VSDX。
- [ ] 确认表格、图表、矩阵和网格的所有子对象可单独选中和编辑。
- [ ] 确认两条语义连接线双端胶合。
- [ ] 回归没有产生文件变化时不创建空提交；若修复了回归问题，将修复文件与对应新增测试提交为 `fix(test-edition): repair component workflow regression`。
