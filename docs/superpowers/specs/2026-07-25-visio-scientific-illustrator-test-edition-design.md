# Visio Scientific Illustrator Test Edition 设计规格

**状态：** 已批准  
**日期：** 2026-07-25  
**目标版本：** `0.4.0-test.1`  
**参考实现：** `icebird1998/scientific-illustrator` 1.3.0，提交 `62ec273ede7570ed04185f6e9d133e495008fe3f`

## 1. 目标

在不修改 Visio Scientific Illustrator 0.3.0 稳定版的前提下，新增一个可与稳定版同时安装的 Test Edition。Test Edition 面向“给定参考图，稳定转换成可编辑 Visio 图”的核心任务，并优先优化以下指标：

1. 低成本模型也能完成主要工作；
2. 结构、文字、连接线和 VSDX 可编辑性保持稳定；
3. 模型调用、图片上下文和自然语言交接显著减少；
4. 复杂但次要的小元素可以作为可追踪的小型图片资产；
5. 不确定或高风险区域自动升级到更严格的模型审查；
6. Codex 和 Claude Code 可以同时安装稳定版与 Test Edition。

Test Edition 借鉴 Scientific Illustrator 1.3.0 的 Designer、Drawer、Reviewer、Corrector 职责划分、能力发现、原子图片声明和局部质量门，但继续使用现有 Visio 后端更成熟的 SceneGraph、可见 COM 绘制、ShapeSheet、真实胶合、语义 ID、预览比较、热图和 VSDX 校验。

## 2. 非目标

本版本不包含：

- 后台或无窗口绘制模式；
- 对稳定版插件目录、清单、Bridge、Skill 或安装器的修改；
- 整页或整面板截图替代可编辑重建；
- 未经声明的第三方图像生成 API；
- PowerPoint 或 draw.io 后端；
- 把所有 Reviewer 和 Corrector 工作强制交给模型；
- 使用复杂论文图进行自动测试或日常模型前向测试。

## 3. 设计原则

### 3.1 完全隔离

Test Edition 使用独立目录、插件 ID、Marketplace、MCP server、公共工具名、Skill、Bridge、缓存和安装器。稳定版仅作为只读能力基线。

### 3.2 工具完成确定性工作

几何、胶合、文字溢出、对齐、间距、边界、元数据、哈希、结构审计和安全修正应由代码完成。模型主要处理科学语义、视觉歧义、参考图理解和无法确定性修复的问题。

### 3.3 不确定时升级

低置信度不能直接通过。确定性工具不能确认的区域必须升级到模型 Reviewer，而不是降低验收标准。

### 3.4 可编辑性分层

内容分为：

- 原生 Visio 对象；
- 由原生 Visio 图元组成的可编辑复合组件；
- 内部像素不可编辑、但整体可移动、缩放、替换和追踪的小型图片资产。

### 3.5 当前证据才能通过

区域通过结论必须绑定当前页面修订号、结构快照哈希、预览哈希和参考图裁剪哈希。页面变化后，旧结论自动失效。

## 4. 仓库与发布隔离

### 4.1 目录

```text
editions/
└─ visio-scientific-illustrator-test/
   ├─ .agents/
   │  └─ plugins/
   │     └─ marketplace.json
   ├─ .claude-plugin/
   │  └─ marketplace.json
   ├─ package.json
   ├─ scripts/
   │  ├─ validate-repo.mjs
   │  └─ smoke-test.mjs
   └─ plugins/
      └─ visio-scientific-illustrator-test/
         ├─ .codex-plugin/
         │  └─ plugin.json
         ├─ .claude-plugin/
         │  └─ plugin.json
         ├─ .mcp.json
         ├─ .mcp.claude.json
         ├─ bin/
         ├─ bridge/
         ├─ scripts/
         ├─ skills/
         ├─ references/
         └─ assets/
```

根目录只新增：

```text
install-test.ps1
install-claude-test.ps1
README.test.md
README.test.en.md
```

### 4.2 身份

| 项目 | 稳定版 | Test Edition |
|---|---|---|
| 显示名称 | Visio Scientific Illustrator | Visio Scientific Illustrator Test |
| 插件 ID | `visio-scientific-illustrator` | `visio-scientific-illustrator-test` |
| Marketplace | `visio-scientific-tools` | `visio-scientific-tools-test` |
| 版本 | `0.3.0` | `0.4.0-test.1` |
| Live MCP | `visio-live` | `visio-test-live` |
| File MCP | `visio-file-utils` | `visio-test-file-utils` |
| 公共工具前缀 | `visio_live_*` | `visio_test_live_*` |
| Bridge | `VisioBridge.exe` | `VisioTestBridge.exe` |

Test Edition 不导入稳定版运行时代码。初始实现可以从稳定版复制，但复制后独立演进。

### 4.3 稳定版保护

发布测试必须保存并比较稳定版受保护文件清单的 SHA-256。任何稳定版文件变化都使 Test Edition 发布失败。

受保护范围至少包括：

```text
plugins/visio-scientific-illustrator/**
.agents/plugins/marketplace.json
.claude-plugin/marketplace.json
install.ps1
install-claude.ps1
```

## 5. 五角色 Skill

### 5.1 总编排

`visio-test-orchestrate-scientific-figure`

职责：

- 识别复刻、设计、审查和修正任务；
- 选择 `strict`、`adaptive` 或 `economy`；
- 创建任务预算和参考图会话；
- 管理区域状态；
- 调用 Designer、Drawer、Reviewer 和 Corrector；
- 确保最终全图质量门和交付验证完成。

总编排是推荐的用户入口。

### 5.2 Designer

`visio-test-design-scientific-figure`

职责：

- 从参考图或研究描述生成一次全局设计；
- 定义区域、对象、坐标、样式 token、连接拓扑和图元预算；
- 为每个对象选择原生、复合或图片资产表达；
- 记录歧义；
- 不执行绘图。

### 5.3 Drawer

`visio-test-draw-scientific-figure-live`

职责：

- 调用能力发现；
- 启动插件拥有的可见 Visio；
- 仅绘制当前状态机允许的区域；
- 执行 SceneGraph、复合组件、图片资产和对象级修正；
- 不批准自己的结果。

### 5.4 Reviewer

`visio-test-audit-scientific-figure`

职责：

- 解释确定性审查和当前预览；
- 审查科学语义、主观视觉和复杂图片拆分；
- 创建稳定 finding；
- 签发或拒绝区域通过；
- 不修改页面。

### 5.5 Corrector

`visio-test-correct-scientific-figure`

职责：

- 将 finding 转换成最小对象级修改计划；
- 标明需要保留的对象；
- 使用确定性安全补丁或精确 Drawer 操作；
- 不批准自己的修正。

### 5.6 渐进披露

每个 SKILL.md 保留角色边界和核心流程。以下内容放入 `references/`：

- `quality-contract.md`
- `finding-taxonomy.md`
- `component-contracts.md`
- `raster-assets.md`
- `tool-workflows.md`
- `quality-profiles.md`

角色间交接使用状态句柄和 JSON，不重复粘贴完整规范。

## 6. 参考图准备

### 6.1 工具

```text
visio_test_prepare_reference
visio_test_get_reference_manifest
visio_test_get_reference_region
visio_test_extract_reference_asset
```

### 6.2 Reference Manifest

参考图只预处理一次。Manifest 包含：

```json
{
  "reference_id": "ref-...",
  "source_path": "E:\\figures\\reference.png",
  "source_sha256": "...",
  "width_px": 1600,
  "height_px": 900,
  "palette": [],
  "foreground_bounds": {},
  "candidate_regions": [],
  "candidate_assets": [],
  "coordinate_transform": {},
  "manifest_sha256": "..."
}
```

预处理层负责：

- 图片尺寸与校验；
- 主色板；
- 前景和边缘摘要；
- 候选区域；
- 紧裁剪文件；
- 候选小元素边界；
- 固定坐标转换；
- 后续证据哈希。

模型默认只读取整图一次，随后按需读取当前区域或小元素裁剪。

### 6.3 参考图坐标

SceneGraph 继续使用参考图顶部左原点像素坐标。Bridge 负责转换到 Visio 页面坐标。模型不重复换算英寸、点或 Visio 内部坐标。

## 7. 元素表达决策

### 7.1 原生对象

必须优先原生表达：

- 可读文字和公式；
- 矩形、圆、线、箭头和面板；
- 语义连接关系；
- 边框、图例和标注；
- 表格文字；
- 规则坐标轴和统计图；
- 可稳定表达的科学组件。

### 7.2 可编辑复合组件

用于：

- 表格；
- 柱状图、折线图、散点图和误差线；
- 热力矩阵；
- 数值或标签矩阵；
- 规则网格；
- 重复科学结构；
- 复杂但规则的图标。

复合组件由独立 Visio 图元构成。父对象和每个子对象都具有稳定语义 ID。

### 7.3 小型图片资产

允许以下来源：

```text
reference_crop
user_file
generated_external
```

插件不自动配置或调用第三方图像生成 API。宿主有图像生成能力时，可以先生成本地文件，再作为 `generated_external` 注册。宿主没有该能力时返回 `CAPABILITY_NOT_AVAILABLE`。

图片资产条件：

- 面积受限；
- 是单一语义元素或模块；
- 整体可移动、缩放、替换和分层；
- 周围文字、箭头、边框和连接关系单独重建；
- 不能是整页或整面板；
- 保存来源、裁剪范围、哈希和可编辑范围。

复杂路由器图标可以作为图片。“图标 + 标题 + 箭头 + 说明文字”不能合并为图片。

### 7.4 图片资产声明

所有图片都要求：

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
  "source_reference_sha256": "...",
  "source_bounds": {},
  "asset_sha256": "...",
  "semantic_role": "wireless_router"
}
```

这里的 `contains_reconstructable_content=false` 表示图片不包含应单独重建的文字、箭头、边框、连接关系、坐标轴、表格或规则图。小型插画本体可以保留为一个不透明图片资产。

## 8. 能力发现

### 8.1 工具

```text
visio_test_live_get_capabilities
visio_test_live_get_capability_details
```

### 8.2 摘要响应

默认返回：

- Visio 版本、build、位数；
- Test Edition 协议版本；
- capability profile/hash；
- 对象家族；
- 组件家族；
- 图片和裁切能力；
- 连接线和连接点能力；
- 图层、容器和组合能力；
- ShapeSheet allowlist 摘要；
- 保存和导出格式；
- 当前限制。

完整 Master、组件、ShapeSheet、格式和限制清单按类别查询。

### 8.3 能力阶段

能力响应区分：

- `installed`：不启动文档即可确认；
- `session`：可见 Visio 会话启动后确认；
- `document`：当前文档或页面相关；
- `unavailable`：明确缺少或未探测。

能力不足必须返回 `CAPABILITY_NOT_AVAILABLE`，不静默替换。

## 9. 区域状态机

每个区域使用：

```text
planned
→ drawing
→ awaiting_audit
→ failed
→ correcting
→ awaiting_audit
→ passed
```

只有当前区域为 `passed`，Bridge 才允许开始下一区域。

任何添加、删除、移动、缩放、样式、文字、组合、图层、z-order、连接点或手工 Visio 修改都会：

1. 增加 `page_revision`；
2. 标记相关区域审查过期；
3. 清除旧通过令牌；
4. 将区域设为 `awaiting_audit`。

写操作可以携带 `expected_revision`。不匹配时返回 `REVISION_CONFLICT`。

## 10. 审查证据

每次审查绑定：

```json
{
  "page_revision": 27,
  "region_id": "encoder",
  "preview_sha256": "...",
  "reference_crop_sha256": "...",
  "structure_snapshot_sha256": "...",
  "audit_profile": "adaptive-v1"
}
```

`finding_id` 由区域、类别和稳定对象 ID 生成，不包含修订号。同一缺陷跨修订保持同一 ID。

观察记录包含 `observed_revision` 和证据哈希。旧证据不能签发通过。

## 11. 双层 Reviewer

### 11.1 Deterministic Reviewer

每个区域必跑，不依赖模型判断：

- 精确文本存在性；
- 文字溢出和裁切；
- 对象越界和非预期重叠；
- 对齐、等距和尺寸一致性；
- 双端胶合；
- 箭头净空和路径穿越；
- 语义 ID、分组、图层和 z-order；
- 图片资产声明；
- 页面修订和证据新鲜度；
- 区域参考图视觉指标。

### 11.2 Model Reviewer

以下条件自动触发：

- hard finding；
- 视觉指标低于保守阈值；
- 参考图文字或科学语义不明确；
- 连接关系复杂或可能方向错误；
- 区域含多个图片资产；
- 复杂图表、矩阵或密集网格；
- 自动修正后仍失败；
- 区域复杂度超过阈值；
- 原生、复合或图片决策置信度不足。

不确定时必须升级，不能直接通过。

## 12. 质量策略

### 12.1 Strict

- 每个区域运行模型 Reviewer；
- 每个失败运行模型 Corrector；
- 最终全图模型审查；
- 用于复杂论文图和最终投稿图。

### 12.2 Adaptive

Test Edition 默认策略：

- 每区 Deterministic Reviewer；
- 第一地区强制模型校准；
- 复杂度最高区域强制模型检查；
- 失败和低置信度区域模型检查；
- 最终全图强制模型审查。

第一地区与最复杂区域相同时，两次检查可以合并。

### 12.3 Economy

- 每区 Deterministic Reviewer；
- 仅失败和低置信度区域运行模型 Reviewer；
- 最终全图模型审查；
- 用于简单图和快速草稿。

## 13. 安全自动修正

finding 可以附带 `safe_patch`：

```json
{
  "operation": "move_connector_waypoint",
  "object_id": "flow-3",
  "expected_revision": 27,
  "parameters": {
    "waypoint_index": 1,
    "x": 420,
    "y": 188
  }
}
```

允许自动修正：

- 对齐；
- 等距；
- 统一尺寸；
- 文字框有限扩展；
- z-order；
- 连接线净空；
- 表格行列尺寸；
- 轻微越界。

禁止自动修正：

- 科学语义；
- 箭头方向；
- 文字内容；
- 图片代表的对象；
- 大范围布局；
- 已通过区域的大批量修改。

所有自动修正必须携带 `expected_revision`，执行后重新审查。

## 14. finding 合同

```json
{
  "finding_id": "connector-route:encoder:flow-3",
  "severity": "hard",
  "category": "connector_clearance",
  "region_id": "encoder",
  "object_ids": ["flow-3", "encoder-box"],
  "observed_revision": 27,
  "evidence": {
    "preview_sha256": "...",
    "measured_clearance_px": -4.2
  },
  "required_change": "Route the connector outside the encoder label.",
  "acceptance_conditions": [
    "Both endpoints remain glued",
    "Minimum clearance is at least 6 px"
  ],
  "safe_patch": null,
  "status": "open"
}
```

严重程度：

```text
hard
warning
info
```

状态：

```text
open
resolved
waived
stale
```

hard finding 不能 waiver。warning waiver 必须记录原因和当前证据。

## 15. 可编辑复合组件

### 15.1 工具

```text
visio_test_live_add_table
visio_test_live_update_table_cell
visio_test_live_update_table_layout

visio_test_live_add_chart
visio_test_live_update_chart

visio_test_live_add_matrix
visio_test_live_update_matrix_cell

visio_test_live_add_grid
visio_test_live_update_grid
```

### 15.2 首版图表

- 柱状图；
- 折线图；
- 散点图；
- 带误差线统计图；
- 热力矩阵；
- 独立图例、坐标轴、刻度和标签。

### 15.3 组件 ID

```text
accuracy-chart
accuracy-chart.axis.x
accuracy-chart.series.method-a.point.1
accuracy-chart.legend.method-a
```

父组件保存组件类型、数据摘要、布局版本和子 ID 清单。子对象保持独立可编辑。

## 16. Visio 持久化

### 16.1 图片

图片声明同时写入：

- User Cells：规范机器数据；
- Shape Data：Visio UI 中可见的人类可读数据。

审查时比较两套值。缺失或不一致产生 hard finding。

### 16.2 对象

每个 Test Edition 对象保存：

```text
User.CodexId
User.VsiEdition = "test"
User.VsiProtocolVersion
User.VsiRegionId
User.VsiObjectRole
User.VsiLastModifiedRevision
```

### 16.3 页面

PageSheet 保存：

- 协议版本；
- 页面修订号；
- 当前区域；
- 当前质量状态；
- 最近通过修订号；
- 预览哈希；
- 结构哈希；
- SolutionXML 哈希。

### 16.4 文档审查历史

区域注册表和有限审查历史写入专属 SolutionXML。PageSheet 保存其 SHA-256。保存重开后：

1. 读取 SolutionXML；
2. 验证哈希；
3. 读取 PageSheet 和对象元数据；
4. 重新生成结构快照；
5. 旧审查只作为历史，不能替代新的最终审查。

## 17. Token 与时间控制

### 17.1 任务预算

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

### 17.2 响应压缩

- capabilities 默认返回摘要和 hash；
- audit 默认返回分数、hard finding 和计数；
- finding 详情按 ID 查询；
- 通过区域返回预览路径、尺寸和 hash，不内联图片；
- 失败区域返回紧裁剪证据；
- 对象清单使用分页和字段选择；
- 角色交接保存为状态句柄；
- 批量布局和复合组件使用高层工具。

### 17.3 质量保险

Adaptive 的目标不是减少所有审查，而是减少重复模型审查。以下指标不得因策略切换下降：

- 主要文字准确率；
- 主要科学语义；
- 连接方向；
- 双端胶合；
- 可编辑对象结构；
- 无严重裁切和重叠；
- 图片来源追踪；
- VSDX 保存重开稳定性。

## 18. 错误合同

新增稳定错误码：

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

错误包含：

```json
{
  "code": "QUALITY_GATE_BLOCKED",
  "message": "Current region has unresolved hard findings.",
  "retryable": false,
  "region_id": "encoder",
  "page_revision": 27,
  "finding_ids": []
}
```

不使用静默 fallback。

## 19. 安全与进程边界

- 仅控制 Test Edition 启动的 Visio 进程；
- 不附着用户已有 Visio；
- 不使用系统鼠标、键盘、窗口坐标或屏幕点击；
- Test Edition 与稳定版分别拥有 COM 进程；
- 关闭、清空和覆盖仍需要明确授权；
- 所有路径必须是绝对路径；
- 参考图、裁剪、VSDX 和预览保留在本机；
- 不开放网络端口；
- 不自动配置外部图像生成服务。

## 20. 测试策略

### 20.1 Node 合同测试

- 双平台清单；
- Test Edition 名称和版本；
- 独立 MCP server 和工具前缀；
- 图片声明 schema；
- capability 摘要和详情；
- finding schema；
- 状态机；
- revision 和 stale token；
- quality profile；
- Reference Manifest；
- 稳定版哈希保护。

### 20.2 .NET 单元测试

- 状态转换；
- finding ID 稳定性；
- 页面修订；
- User Cells、Shape Data、PageSheet 和 SolutionXML；
- 表格、图表、矩阵和网格计划；
- 安全补丁；
- 过期证据拒绝；
- 错误码。

### 20.3 简单 Visio 集成图

固定使用约 `800 × 500` 的两区域测试图：

- 2 个区域；
- 8–12 个基础对象；
- 2 条双端胶合连接线；
- 一个 `2 × 3` 表格；
- 一个三柱统计图；
- 一个小型原子图片；
- 一个故意制造的连接线净空问题；
- 一次 Reviewer → Corrector → Reviewer 闭环。

不使用复杂论文图作为自动测试。

### 20.4 模型前向测试

最多：

- 一张简单参考图；
- 两个区域；
- 12 个左右对象；
- 一次纠正循环；
- Strict 一次；
- Adaptive 一次。

记录：

- 模型；
- 推理等级；
- 输入和输出 token；
- MCP 调用数；
- 图片进入上下文次数；
- 耗时；
- finding；
- 自动修正数；
- 最终视觉、结构和文件指标。

## 21. A/B 验收目标

Adaptive 相比 Strict 的发布目标：

- 模型图片审查次数至少减少 50%；
- 工具返回 token 至少减少 40%；
- 总时间至少减少 30%；
- 结构、胶合和可编辑性指标不下降；
- 最终视觉审查得分不低于 Strict 的 90%；
- 参考图中可读且无歧义的主要语义和文字准确率保持 100%。

如果简单测试未达到目标，Adaptive 不作为默认策略，Test Edition 默认 Strict。

## 22. 安装

### 22.1 Codex

```powershell
.\install-test.ps1
```

安装：

```text
visio-scientific-illustrator-test@visio-scientific-tools-test
```

### 22.2 Claude Code

```powershell
.\install-claude-test.ps1
```

安装后执行 `/reload-plugins` 或新建会话。

### 22.3 并存

稳定版和 Test Edition 同时启用。Test Edition 测试必须显式选择 **Visio Scientific Illustrator Test**。

Claude Code 推荐显式调用：

```text
/visio-scientific-illustrator-test:visio-test-orchestrate-scientific-figure
```

## 23. 发布

发布标签：

```text
v0.4.0-test.1
```

发布前必须验证：

1. 稳定版受保护文件 hash 未变化；
2. Codex 报告两个插件均已安装和启用；
3. Claude Code 报告两个插件均已安装和启用；
4. MCP server、工具名、Skill 和缓存目录无冲突；
5. 简单 Visio 集成图通过；
6. Strict 与 Adaptive A/B 结果满足默认策略要求；
7. 完整自动测试通过；
8. 独立代码审查没有 Critical 或 Important 问题。

## 24. 实施阶段

1. 创建完全隔离的 Test Edition 骨架并建立稳定版哈希保护；
2. 重命名插件、MCP、工具和 Bridge 身份；
3. 拆分五角色 Skill 和引用资料；
4. 实现能力发现；
5. 实现图片资产声明、裁剪、注册和持久化；
6. 实现页面修订、区域状态机和审查证据；
7. 实现确定性审查和 finding；
8. 实现安全自动修正；
9. 实现表格、图表、矩阵和网格；
10. 实现 Strict、Adaptive 和 Economy；
11. 完成简单测试、A/B 测试和 token 统计；
12. 完成双平台安装器、文档、安装和预发布验证。

## 25. 最终验收

Test Edition 完成的定义：

- 用户给出一张参考图和输出路径；
- 低成本模型可以通过总编排 Skill 完成参考图预处理、规划、可见绘制、局部审查、必要修正和最终交付；
- 输出 VSDX 中的主要文字、形状、连接线、表格、图表、矩阵和网格可编辑；
- 小型图片资产有来源、裁剪、哈希和可编辑范围；
- 所有语义连接线双端胶合；
- 当前区域未通过时无法进入下一区域；
- 旧预览不能被用于宣布通过；
- 保存重开后语义 ID、质量状态、图片声明和主要结构仍可审计；
- Stable 0.3.0 保持不变；
- Codex 和 Claude Code 可以同时安装并明确选择两个版本。
