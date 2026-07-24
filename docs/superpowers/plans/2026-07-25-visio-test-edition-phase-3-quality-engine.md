# Test Edition Phase 3：修订、区域质量门与确定性审查实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans`, `test-driven-development`, and `systematic-debugging` for any failing integration test. Complete Phases 1–2 first.

**Goal:** 将“画完看一眼”改造成 Bridge 强制执行的修订化质量状态机，使旧截图、旧结构和未解决 hard finding 无法让区域通过。

**Architecture:** C# Bridge 是 page revision、区域状态和 finding 的权威写入者。Node MCP 只做 schema、紧凑响应和工具编排。PageSheet 保存当前状态，SolutionXML 保存有界历史，所有审查绑定当前 preview/reference/structure hash。

**Tech Stack:** .NET 8、Visio COM、PageSheet、SolutionXML、SHA-256、Node MCP。

**Path Key:** 下文 `TEST_PLUGIN_ROOT` 严格表示仓库相对路径 `editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test`。

---

## Task 1：定义修订、状态和错误合同

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/QualityContracts.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/AuditContracts.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/BridgeErrorCodes.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/RegionStateMachine.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/RegionStateMachineTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/quality-contract.test.mjs`

- [ ] **Step 1：写失败测试**

状态只允许：

```text
planned -> drawing -> awaiting_audit
awaiting_audit -> failed -> correcting -> awaiting_audit
awaiting_audit -> passed
passed -> awaiting_audit
```

禁止 `drawing -> passed`、`failed -> passed`、`passed -> drawing`。非法转换返回 `AUDIT_STATE_CORRUPT`。

新增错误码逐字匹配：

```text
QUALITY_GATE_BLOCKED
STALE_AUDIT_EVIDENCE
REVISION_CONFLICT
RASTER_DECLARATION_INVALID
COMPONENT_CONTRACT_INVALID
REFERENCE_CROP_INVALID
CAPABILITY_NOT_AVAILABLE
REFERENCE_MANIFEST_INVALID
ASSET_PROVENANCE_INVALID
AUDIT_STATE_CORRUPT
```

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/quality-contract.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter RegionStateMachineTests
```

- [ ] **Step 3：实现纯状态机**

`RegionStateMachine` 不访问 COM，只接收当前状态、事件和 revision，返回新状态或明确异常。事件固定为：

```text
begin_drawing
finish_drawing
audit_failed
begin_correction
finish_correction
audit_passed
page_mutated
```

hard finding 存在时 `audit_passed` 必须拒绝；warning waiver 必须有非空原因与当前证据。

- [ ] **Step 4：运行测试并提交**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/quality-contract.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter RegionStateMachineTests
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): define revisioned region states"
```

## Task 2：实现 PageSheet 和对象修订元数据

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Persistence/PageQualityStateStore.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Persistence/ShapeMetadataService.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/CommandRouter.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/PageQualityStateStoreTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.IntegrationTests/PageRevisionIntegrationTests.cs`

- [ ] **Step 1：写失败测试**

PageSheet 固定 cells：

```text
User.VsiProtocolVersion
User.VsiPageRevision
User.VsiCurrentRegion
User.VsiQualityState
User.VsiLastPassedRevision
User.VsiPreviewSha256
User.VsiStructureSha256
User.VsiSolutionXmlSha256
```

每个 Test 对象固定 cells：

```text
User.CodexId
User.VsiEdition
User.VsiProtocolVersion
User.VsiRegionId
User.VsiObjectRole
User.VsiLastModifiedRevision
```

- [ ] **Step 2：运行失败测试**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter PageQualityStateStoreTests
```

- [ ] **Step 3：集中处理所有写操作**

将以下写操作统一包在 `MutationScope`：

```text
add/update/delete shape
add/update path
add connector/connection point
group/ungroup/container
z-order/layer/layout/size/protection
scientific/composite component
clear
scene execution step
```

`MutationScope` 要求：

1. 可选 `expected_revision` 与当前 revision 比较；
2. COM 操作成功后 revision 只增加一次；
3. 写对象元数据；
4. 将受影响区域设为 `awaiting_audit`；
5. 清除旧 pass token；
6. COM 操作失败时 revision 不增加。

不要为每个复合组件子 shape 各增一次 revision；一次公开工具调用成功只产生一次页面 revision。

- [ ] **Step 4：检测用户手工修改**

在每次审查和下一次写操作前生成结构摘要。若摘要与 PageSheet 中最近结构 hash 不同，视为外部修改：

- revision 增加一次；
- 所有无法精确归属的区域变为 `awaiting_audit`；
- 旧 pass token 失效；
-返回 `external_change_detected=true`。

- [ ] **Step 5：运行单元与集成测试**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter PageQualityStateStoreTests
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter PageRevisionIntegrationTests
```

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): persist page and object revisions"
```

## Task 3：实现结构快照、证据包和过期拒绝

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/StructureSnapshotBuilder.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditEvidenceService.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Export/PreviewExporter.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/StructureSnapshotBuilderTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/AuditEvidenceServiceTests.cs`

- [ ] **Step 1：写确定性 hash 测试**

结构快照按 semantic ID 排序，并只包含审查相关字段：

```text
semantic_id, kind, bounds, text, style summary, group parent,
layer, z-order, connector endpoints, glue state, image declaration hash,
component type, region_id, last_modified_revision
```

浮点值统一四位小数，颜色统一 `#RRGGBB`，null 字段省略。对象枚举顺序变化不能改变 hash；任一结构字段变化必须改变 hash。

- [ ] **Step 2：实现审查证据**

证据包固定为：

```json
{
  "page_revision": 27,
  "region_id": "encoder",
  "preview_sha256": "64 lowercase hex characters",
  "reference_crop_sha256": "64 lowercase hex characters",
  "structure_snapshot_sha256": "64 lowercase hex characters",
  "audit_profile": "adaptive-v1"
}
```

预览必须在审查开始时由当前页导出；不得接受调用者传入的 preview hash。reference crop hash 必须来自 Phase 2 的 Reference Manifest。

- [ ] **Step 3：实现 stale 检查**

签发 pass 前重新读取 page revision 和 structure hash。任一字段与观察证据不同，返回：

```json
{
  "code": "STALE_AUDIT_EVIDENCE",
  "retryable": true,
  "observed_revision": 27,
  "current_revision": 28
}
```

不得把 stale finding 标成 resolved。

- [ ] **Step 4：运行测试并提交**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter "StructureSnapshotBuilderTests|AuditEvidenceServiceTests"
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): bind audits to current evidence"
```

## Task 4：实现 finding 和确定性 Reviewer

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/FindingId.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/DeterministicFigureAuditor.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/ExactTextRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/TextOverflowRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/CanvasBoundsRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/UnexpectedOverlapRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/AlignmentSpacingRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/ConnectorGlueRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/ConnectorClearanceRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/SemanticMetadataRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/RasterDeclarationRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/EvidenceFreshnessRule.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/AuditRules/RegionalVisualMetricRule.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Abstractions/IVisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Commands/CommandRouter.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/FindingIdTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/DeterministicFigureAuditorTests.cs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/audit-tool.test.mjs`

- [ ] **Step 1：写失败测试**

公开工具：

```text
visio_test_live_audit_figure
visio_test_live_get_audit_findings
visio_test_live_get_audit_finding
visio_test_live_set_audit_decision
```

finding ID 公式：

```text
lowercase(category) + ":" + lowercase(region_id) + ":" +
join(",", ordinal-sorted lowercase object_ids)
```

修订号不进入 finding ID。相同缺陷跨 revision 保持 ID。

- [ ] **Step 2：实现首批审查规则**

独立规则文件：

```text
ExactTextRule.cs
TextOverflowRule.cs
CanvasBoundsRule.cs
UnexpectedOverlapRule.cs
AlignmentSpacingRule.cs
ConnectorGlueRule.cs
ConnectorClearanceRule.cs
SemanticMetadataRule.cs
RasterDeclarationRule.cs
EvidenceFreshnessRule.cs
RegionalVisualMetricRule.cs
```

每条 finding 必须包含 severity、category、region、object IDs、observed revision、evidence、required change、acceptance conditions、safe patch 或 null、status。

- [ ] **Step 3：限制默认响应**

`audit_figure` 默认只返回：

- overall/region score；
- hard/warning/info 数量；
- hard finding 摘要；
- page revision 和四个证据字段；
- `model_review_required` 与触发原因；
- pass 是否可签发。

finding 详情必须按 ID 查询。通过区域只返回预览路径和 hash，不内联图片；失败区域允许返回一个紧裁剪证据图。

- [ ] **Step 4：实现质量门**

开始下一区域、`draw_scene` 跨区域或签发 pass 前，Bridge 检查当前区域：

- 状态为 `passed`；
- pass revision 等于当前 revision；
-无 open hard finding；
-证据 hash 当前有效。

否则返回 `QUALITY_GATE_BLOCKED`，包含 region、revision 和 finding IDs。

- [ ] **Step 5：运行测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/audit-tool.test.mjs
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter "FindingIdTests|DeterministicFigureAuditorTests"
```

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): add deterministic figure audits"
```

## Task 5：实现安全补丁

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/SafePatchValidator.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Quality/SafePatchExecutor.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Contracts/AuditContracts.cs`
- Modify: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Session/VisioSession.cs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/SafePatchTests.cs`

- [ ] **Step 1：写 allowlist 测试**

允许：

```text
align_shapes
distribute_shapes
match_size
expand_text_box
set_z_order
move_connector_waypoint
resize_table_row
resize_table_column
move_inside_canvas
```

禁止：

```text
change_text
reverse_connector
replace_image
rebuild_region
move_passed_region
```

所有 patch 必须包含 `expected_revision` 和 object IDs。非 allowlist 返回 `COMPONENT_CONTRACT_INVALID` 或 `INVALID_ARGUMENT`，不得执行。

- [ ] **Step 2：实现执行器**

执行前验证 finding 仍 open、revision 当前、对象属于该 finding。成功后：

- revision 增加一次；
- finding 变为 stale；
-区域进入 `awaiting_audit`；
-立即要求重新审查；
-不自动签发 pass。

- [ ] **Step 3：运行测试并提交**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.Tests/VisioTestBridge.Tests.csproj -c Release --filter SafePatchTests
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): apply revision-safe audit patches"
```

## Task 6：持久化有限审查历史

**Files:**

- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Core/Persistence/AuditSolutionXmlStore.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.Tests/AuditSolutionXmlStoreTests.cs`
- Create: `TEST_PLUGIN_ROOT/bridge/VisioTestBridge.IntegrationTests/AuditPersistenceIntegrationTests.cs`

- [ ] **Step 1：写失败测试**

SolutionXML solution 名固定为：

```text
VisioScientificIllustratorTest.Audit.v1
```

保存：

- region registry；
-最近 20 次区域审查；
-最近 5 次全图审查；
-finding 当前状态与 waiver；
-profile 和证据 hash。

超过上限按 revision 由旧到新裁剪。PageSheet `User.VsiSolutionXmlSha256` 必须匹配规范 XML 的 SHA-256。

- [ ] **Step 2：实现写入、读取和损坏拒绝**

读取时 hash 不匹配返回 `AUDIT_STATE_CORRUPT`。不得忽略损坏后创建空历史。重开文档后，历史仅作审计记录；必须重新生成当前结构快照，旧 pass 不能替代最终审查。

- [ ] **Step 3：运行集成测试**

```powershell
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter AuditPersistenceIntegrationTests
```

- [ ] **Step 4：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "feat(test-edition): persist bounded audit history"
```

## Task 7：阶段回归

- [ ] 运行：

```powershell
npm --prefix editions/visio-scientific-illustrator-test run test:node
npm --prefix editions/visio-scientific-illustrator-test run test:bridge
dotnet test editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/bridge/VisioTestBridge.IntegrationTests/VisioTestBridge.IntegrationTests.csproj -c Release --filter "PageRevisionIntegrationTests|AuditPersistenceIntegrationTests"
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
git diff --check
```

- [ ] 人工验证一条 stale 路径：导出审查预览 → 手工移动一个 Test shape → 尝试用旧证据通过。Expected: `STALE_AUDIT_EVIDENCE`。

- [ ] 回归没有产生文件变化时不创建空提交；若修复了回归问题，将修复文件与对应新增测试提交为 `fix(test-edition): repair quality gate regression`。
