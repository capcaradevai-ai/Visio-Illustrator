# Visio Scientific Illustrator Test Edition 总实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan phase by phase. Do not modify implementation code until the user explicitly approves execution.

**Goal:** 在不改变稳定版 `0.3.0` 的前提下，在同一仓库新增完全隔离的 `Visio Scientific Illustrator Test 0.4.0-test.1`，使低成本模型能够通过工具主导的可见 Visio 工作流，稳定地把参考图重建为可编辑 VSDX。

**Architecture:** Test Edition 位于 `editions/visio-scientific-illustrator-test/`，拥有独立 Marketplace、插件目录、MCP 名称、工具前缀、Bridge 可执行文件、Skill、测试和发布脚本。实现顺序遵循“隔离边界 → 能力与参考图 → 质量状态机 → 复合组件与角色 Skill → 双平台安装和 A/B 发布验收”。任何阶段都不得导入稳定版运行时代码。

**Tech Stack:** Node.js 22、MCP、Node Test Runner、.NET 8、C#、Microsoft Office Interop Visio、Visio ShapeSheet、SolutionXML、PowerShell、Codex 插件清单、Claude Code 插件清单。

---

## 1. 权威输入

- 设计规格：`docs/superpowers/specs/2026-07-25-visio-scientific-illustrator-test-edition-design.md`
- 稳定版插件：`plugins/visio-scientific-illustrator/`
- 稳定版版本：`0.3.0`
- Test Edition 版本：`0.4.0-test.1`
- 参考实现基线：`icebird1998/scientific-illustrator` `1.3.0`，分析基准提交 `62ec273ede7570ed04185f6e9d133e495008fe3f`

设计规格优先于旧计划和稳定版行为。若实施中发现规格与 Microsoft Visio 实际能力冲突，停止当前任务，记录可复现证据并请求用户决定；不得添加静默 fallback。

## 2. 分阶段计划

| 顺序 | 计划 | 产物 | 阶段出口 |
|---|---|---|---|
| 1 | [隔离骨架与协议身份](2026-07-25-visio-test-edition-phase-1-isolation.md) | 独立目录、Test Bridge、独立 MCP、稳定版哈希保护 | Stable 哈希不变，Test MCP 可独立启动 |
| 2 | [能力发现、参考图与图片资产](2026-07-25-visio-test-edition-phase-2-reference-capabilities.md) | capability profile、Reference Manifest、原子图片声明与持久化 | 能力和资产可查询、保存、重开、审计 |
| 3 | [修订、区域质量门与确定性审查](2026-07-25-visio-test-edition-phase-3-quality-engine.md) | 状态机、证据哈希、finding、安全补丁、审查历史 | 旧证据不能通过，失败区域阻止后续绘制 |
| 4 | [复合组件、五角色 Skill 与质量配置](2026-07-25-visio-test-edition-phase-4-components-skills.md) | 表格/图表/矩阵/网格、五角色 Skill、strict/adaptive/economy | 简单两区域图完成一次修正闭环 |
| 5 | [双平台安装、A/B 验收与预发布](2026-07-25-visio-test-edition-phase-5-release.md) | Codex/Claude 安装器、中英文 README、A/B 报告、发布检查 | 两版本并存，全部发布门通过 |

阶段必须顺序执行。每个阶段内部遵循测试驱动开发：先加入会失败的合同测试，再实现最小功能，再运行该任务相关测试，最后运行阶段回归测试并提交。

## 3. 不可变边界

### 3.1 稳定版保护范围

以下路径在所有实施阶段前后必须保持 SHA-256 不变：

```text
plugins/visio-scientific-illustrator/**
.agents/plugins/marketplace.json
.claude-plugin/marketplace.json
install.ps1
install-claude.ps1
```

允许在仓库根目录新增且仅新增：

```text
install-test.ps1
install-claude-test.ps1
README.test.md
README.test.en.md
```

`.neuralmemory/` 是本机未跟踪目录，不得暂存、删除或写入发布文件。

### 3.2 身份合同

| 字段 | 固定值 |
|---|---|
| 插件 ID | `visio-scientific-illustrator-test` |
| 显示名称 | `Visio Scientific Illustrator Test` |
| Marketplace | `visio-scientific-tools-test` |
| 版本 | `0.4.0-test.1` |
| Live MCP | `visio-test-live` |
| File MCP | `visio-test-file-utils` |
| Live 工具前缀 | `visio_test_live_` |
| 文件工具前缀 | `visio_test_` |
| Bridge | `VisioTestBridge.exe` |
| Edition 标记 | `test` |
| 初始协议版本 | `0.4.0-test.1` |

Test Edition 的 JavaScript 模块、C# 程序集、命名空间、进程名、临时目录前缀和日志标签不得使用会与稳定版冲突的运行时身份。

### 3.3 运行边界

- 只控制 Test Edition 自己启动的可见 Visio 进程。
- 不附着用户已打开的 Visio 实例。
- 不使用系统鼠标、键盘、屏幕坐标或 UI 自动点击。
- 不先生成 VSDX 再打开模拟实时绘制。
- 本轮不实现后台或隐藏 Visio 模式。
- 不开放网络端口，不自动调用第三方图片 API。

## 4. 质量门

### Gate A：隔离

- 稳定版受保护文件 hash 完全一致。
- Test Edition 的清单、MCP、工具、Bridge、缓存路径无稳定版名称冲突。
- Stable 与 Test Bridge 可以分别启动并拥有不同 PID。

### Gate B：协议和持久化

- 所有写操作接受可选 `expected_revision`。
- 页面变化递增 `page_revision`，旧通过令牌立即失效。
- 图片声明同时存在于 User Cells 和 Shape Data。
- 页面状态存在于 PageSheet，有限审查历史存在于 SolutionXML。
- 保存、关闭、重开后可重建结构快照并验证 SolutionXML hash。

### Gate C：可编辑性

- 主要文字、形状、连接线、表格、图表、矩阵和网格由独立 Visio 图元组成。
- 语义连接线双端真实胶合。
- 图片仅允许单一语义的小型原子单元，且声明完整。
- 整页、整面板、包含应重建文字/箭头/表格/坐标轴的截图不能通过审查。

### Gate D：质量与成本

- 每区确定性审查必跑。
- Strict 每区模型审查；Adaptive 只在规定触发点审查；两者最终全图模型审查必跑。
- Adaptive 相比 Strict：模型图片审查次数减少至少 50%，工具返回 token 减少至少 40%，总时间减少至少 30%。
- 结构、胶合、可编辑性不下降；视觉分不低于 Strict 的 90%；清晰可读的主要语义和文字准确率为 100%。
- 未达到 A/B 目标时，Test Edition 默认配置必须是 Strict。

### Gate E：发布

- Node、.NET 单元测试和简单 Visio 集成测试通过。
- Codex 和 Claude Code 同时显示稳定版与 Test Edition 已安装且启用。
- 独立代码审查无 Critical 或 Important finding。
- 只有以上门全部通过，才允许创建 `v0.4.0-test.1` 标签。

## 5. 规格追踪矩阵

| 设计规格 | 实施计划 | 主要自动证据 |
|---|---|---|
| §4 仓库与发布隔离 | Phase 1 | `stable-protection.test.mjs`、`identity-isolation.test.mjs` |
| §5 五角色 Skill | Phase 4 Task 6 | `skill-boundaries.test.mjs` |
| §6 参考图准备 | Phase 2 Task 2 | `reference-manifest.test.mjs` |
| §7 元素表达与图片声明 | Phase 2 Tasks 3–4、Phase 4 Tasks 1–4 | raster/component 单元与集成测试 |
| §8 能力发现 | Phase 2 Task 1 | capability Node/.NET 测试 |
| §9–10 状态机和证据 | Phase 3 Tasks 1–3 | revision、snapshot、stale 测试 |
| §11 双层 Reviewer | Phase 3 Task 4、Phase 4 Task 5 | audit 与 review-policy 测试 |
| §12 质量策略 | Phase 4 Task 5、Phase 5 Task 6 | profile 测试与 A/B 摘要 |
| §13 安全自动修正 | Phase 3 Task 5 | `SafePatchTests` |
| §14 finding 合同 | Phase 3 Task 4 | finding ID 与 audit tool 测试 |
| §15 复合组件 | Phase 4 Tasks 1–4 | table/chart/matrix/grid 测试 |
| §16 Visio 持久化 | Phase 2 Task 4、Phase 3 Tasks 2/6 | 保存重开集成测试 |
| §17 Token 与时间 | Phase 4 Task 5、Phase 5 Tasks 5–6 | telemetry 与 A/B 报告 |
| §18 错误合同 | Phase 3 Task 1 | quality contract 测试 |
| §19 安全边界 | Phase 1、Phase 5 Task 7 |身份和进程隔离 smoke |
| §20–21 测试与 A/B | Phase 4 Task 7、Phase 5 Task 6 | simple loop 与 A/B 报告 |
| §22–23 安装与发布 | Phase 5 |安装器测试、双平台并存检查 |

## 6. 固定测试图与成本上限

自动集成测试只使用一张 `800 × 500` 的两区域图：

- 8–12 个基础对象；
- 2 条双端胶合连接线；
- 1 个 `2 × 3` 表格；
- 1 个三柱统计图；
- 1 个小型原子图片；
- 1 个故意制造的连接线净空缺陷；
- 1 次 Reviewer → Corrector → Reviewer 闭环。

模型 A/B 只允许同一张简单图各运行一次 Strict 和 Adaptive；每次最多两个区域、一次纠正和一次最终全图审查。失败时优先检查工具日志、结构快照和确定性 finding，不扩大图复杂度。

## 7. 每阶段通用执行规程

- [ ] 开始阶段前运行 `git status --short`，确认只有计划允许的变更。
- [ ] 运行稳定版 hash 校验，预期输出 `Stable protected files unchanged.`
- [ ] 先写失败测试，并记录失败原因与当前任务一致。
- [ ] 只修改当前任务列出的 Test Edition 文件。
- [ ] 运行任务级测试，预期全部通过。
- [ ] 运行阶段回归测试。
- [ ] 搜索 `TODO|TBD|PLACEHOLDER|visio_live_|VisioBridge.exe`，确认没有遗留占位符或错误身份。
- [ ] 检查新文本文件可按 UTF-8 严格解码，且不含 `�`。
- [ ] 复查 `git diff --check`。
- [ ] 复查受保护文件 hash。
- [ ] 仅暂存当前阶段文件并提交。

阶段最终功能提交消息目标如下；各任务已产生提交且回归没有引入新变化时，不创建空提交：

```text
feat(test-edition): isolate plugin and protocol
feat(test-edition): add reference and capability services
feat(test-edition): enforce revisioned quality gates
feat(test-edition): add editable components and role skills
feat(test-edition): package dual-platform test release
```

## 8. 完成定义

- [ ] 用户给出参考图与输出路径后，总编排 Skill 可以完成参考图准备、设计、可见绘制、区域审查、必要修正、最终审查、VSDX 保存、校验和 PNG 导出。
- [ ] 当前区域未通过时，Bridge 拒绝进入下一区域。
- [ ] 页面变化后，旧预览和旧审查不能签发通过。
- [ ] 保存重开后，语义 ID、图片声明、区域状态和审查历史可验证。
- [ ] Test Edition 不依赖稳定版运行时文件。
- [ ] Stable `0.3.0` 的受保护文件保持逐字节不变。
- [ ] Codex 与 Claude Code 均可同时安装、明确选择和卸载 Test Edition，不影响稳定版。

## 9. 高风险实现点与处理方式

| 风险 | 处理方式 | 禁止做法 |
|---|---|---|
| 复合组件创建一半后 COM 失败 | 单一 Visio undo scope，失败整次撤销，成功只增一次 revision | 保留半成品后继续 |
| 用户在 Visio 中手工修改 | 审查和写操作前比较结构 hash，检测到后使相关 pass 过期 | 假设只有 MCP 会修改页面 |
| 旧截图被模型重复使用 | pass 同时校验 revision、preview、reference crop、structure 四项证据 | 只比较文件名或时间戳 |
| 全局 revision 导致历史区域过期 | 区域 pass 用于进入下一地区；最终交付前重新做全页当前证据审查 | 每画一个新区域就重复模型审查全部旧区域 |
| SolutionXML 损坏或被外部修改 | PageSheet 保存规范 XML hash；不一致返回 `AUDIT_STATE_CORRUPT` | 清空历史并静默继续 |
| 图片解码引入 npm 原生依赖 | 使用 self-contained Test Bridge 内的 Windows 图像服务 | 安装时联网下载 `sharp` |
| 图片掩盖可重建内容 | schema、双重持久化和 hard audit rule 三层强制 | 依赖 Skill 文案自觉 |
| Visio group 影响子对象编辑 | 保持子 semantic ID 和 shape，集成测试逐个选取/修改子对象 | flatten、OLE 或整组截图 |
| 新插件安装后当前任务无法发现 MCP | 保存计划和 A/B manifest，重载插件后从明确 checkpoint 继续 | 用稳定版工具名代替 Test 工具 |
| 宿主 token 不可读取 | A/B 标记 `incomplete`，Adaptive 不得成为默认 | 用估算 token 冒充真实模型 token |
| 稳定版确需未来升级 | 新开稳定版发布任务并显式重建 hash 基线 | 在 Test 实施中顺手修改稳定版 |
