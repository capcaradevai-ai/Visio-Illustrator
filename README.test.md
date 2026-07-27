# Visio Scientific Illustrator Test

[English](README.test.en.md) | [稳定版说明](README.md)

Test Edition 是与稳定版完全独立、可并存安装的质量实验版。它仍然只通过 Visio COM 控制插件自己启动的可见 Microsoft Visio 窗口，逐步绘制原生、可编辑图元；在稳定版能力之上增加动态能力发现、五角色工作流、页面 revision、区域质量门、确定性审计、原子栅格声明，以及可编辑表格、统计图、矩阵和网格。

当前版本：`0.4.0-test.1`。当前默认质量配置为 `strict`。在取得带真实宿主 token 的完整 A/B 结果前，不会把 `adaptive` 宣布为默认。

## 系统要求

- Windows 10/11 x64
- Microsoft Visio 桌面版，且 COM 已注册
- Node.js 22 或更高版本
- .NET 8 SDK（从源码构建独立 Bridge；生成的 Bridge 为自包含程序）
- Codex 桌面版/CLI，或 Claude Code 2.1.218 及以上

Test 与 Stable 使用不同的 Marketplace、插件 ID、MCP 名称、Bridge 可执行文件、进程和临时目录：

| 项目 | Stable | Test |
|---|---|---|
| Marketplace | `visio-scientific-tools` | `visio-scientific-tools-test` |
| 插件 | `visio-scientific-illustrator` | `visio-scientific-illustrator-test` |
| 实时 MCP | `visio-live` | `visio-test-live` |
| 文件 MCP | `visio-file-utils` | `visio-test-file-utils` |

卸载或更新 Test 不应修改 Stable。

## 安装与更新

先在仓库根目录构建独立 Bridge：

```powershell
npm --prefix .\editions\visio-scientific-illustrator-test run build:bridge
```

Codex：

```powershell
powershell -ExecutionPolicy Bypass -File .\install-test.ps1
```

Claude Code：

```powershell
powershell -ExecutionPolicy Bypass -File .\install-claude-test.ps1
```

两个安装器均可重复执行用于更新。安装器先校验 Stable 受保护文件、Test 清单、Node/Visio/Bridge 环境，只注册 Test ID。安装后重启 Codex；Claude Code 执行 `/reload-plugins` 或新建会话。

验证：

```powershell
codex plugin list --json
claude plugin list --json
```

Claude Code 的显式入口为：

```text
/visio-scientific-illustrator-test:visio-test-orchestrate-scientific-figure
```

## 推荐用法

Codex 中直接说明使用 `Visio Scientific Illustrator Test`。Claude Code 中先调用上述显式入口，再给出任务。

推荐提示词：

```text
使用 Visio Scientific Illustrator Test，以 strict 模式在可见的 Microsoft Visio
页面中逐步重绘这张参考图。默认每步间隔 500 ms；所有可重建文字、形状、表格、
图表和连接线保持可编辑，连接线端点真正胶合。每个区域通过当前 revision 的审计后
再继续；完成后保存 VSDX、校验文件并导出宽度 2000 px 的 PNG。
```

首次调用先使用 `visio_test_live_get_capabilities`；只有需要具体 Masters、组件或导出限制时再用分页详情工具。绘制过程中每个 checkpoint 会导出当前预览并暂停。如果当前区域未通过审计，Bridge 会拒绝进入下一区域。审计证据由工具从当前页面生成并绑定 preview hash、结构 hash 与 page revision，不能用旧截图放行。

确定性视觉审计请使用 PNG 参考图；JPEG 仍可用于参考资源准备和小型原子素材提取。如果区域通过后有人直接在可见 Visio 页面中手工编辑，Bridge 会检测结构变化、递增页面 revision、作废旧证据并重新关闭区域门禁，直到新审计通过。

## 质量模式

- `strict`：每个区域执行 Designer → Drawer → Reviewer → Corrector，适合模板高度敏感、投稿级图件。
- `adaptive`：首区校准、最复杂区抽查、异常升级、最终全图审查；预期减少模型看图次数，但完整宿主 token A/B 尚未完成。
- `economy`：主要依赖确定性结构审计，只对不确定区域调用模型；适合草稿和低成本批量任务。

五个 Skill 分别负责总编排、设计、Visio 绘制、审查和纠正。模型负责语义与主观视觉判断；能力查询、结构检查、胶合检查、revision、finding ID、图像声明和文件校验由工具执行。

## 可编辑边界

文字、基础形状、路径、连接线、表格单元格、统计图轴/刻度/数据点、矩阵和网格均由独立 Visio 图元组成。没有合适原生对象时仍使用可编辑图元组合，不能用整块截图替代。

只有不可合理重建的小型原子视觉单元可作为图片导入，并必须声明：

- `raster_reason`
- `source_is_tightly_cropped=true`
- `atomic_raster_unit=true`
- `contains_reconstructable_content=false`
- `decomposition_note`

声明会写入 User Cells 与 Shape Data，保存和重开 VSDX 后仍可审计。图片不能包含可重建文字、箭头、表格、坐标轴或图例。

## 常见错误

- `QUALITY_GATE_BLOCKED`：当前区域尚未通过当前 revision 的审计。
- `STALE_AUDIT_EVIDENCE`：页面在审计证据生成后发生变化，需要重新导出并审计。
- `REVISION_CONFLICT`：更新工具使用了旧 revision。
- `RASTER_DECLARATION_INVALID`：图片不是合规的原子栅格单元。
- `COMPONENT_CONTRACT_INVALID`：表格、图表、矩阵或网格合同不合法。
- `CAPABILITY_NOT_AVAILABLE`：当前 Visio、文档或 Master 不支持请求能力。

插件不使用系统鼠标键盘自动化，不接受“先后台生成 VSDX 再打开”的实时绘制替代方案。参考图、预览、VSDX 和统计文件都保留在本机；轻量统计不记录图片字节、完整提示词或完整文件路径。
