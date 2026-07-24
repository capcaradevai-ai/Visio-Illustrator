# Changelog

## 0.3.0 - 2026-07-24

- Added native Claude Code marketplace and plugin manifests.
- Added a Claude Code MCP configuration with cache-safe plugin-root paths.
- Added an audited Windows installer for Claude Code.
- Made the Codex installer update in place without deleting a cache used by a running app.
- Kept one shared Visio Skill, MCP implementation, COM bridge, and asset set for both platforms.
- Added bilingual Codex and Claude Code installation, usage, update, uninstall, and troubleshooting guides.
- Added dual-platform packaging and installer contract tests.

## 0.2.0 - 2026-07-23

- 新增 Scientific SceneGraph、区域检查点和可见逐步执行，支持从指定区域继续绘制。
- 新增 draw.io 蓝图编译，保留节点、边、富文本、渐变、旋转、折线路由和装饰路径，不先生成 VSDX。
- 新增可编辑 Visio Geometry 自由路径、字符级富文本、科学组件、布局、图层、Z-order、保护和安全 ShapeSheet 接口。
- 扩展连接线锚点、具名连接点和折线路由；语义连接线保存后校验 BeginX/EndX 双端 GlueTo。
- 新增结构保真校验和整图/分区域视觉比较，输出边缘、覆盖率、颜色差异及可选热图。
- 重写 Visio Skill，移除 draw.io 专属术语与约束，加入高保真拆图、图元预算、区域纠偏和禁止简化规则。
- 补充完整 API 示例、Visio 版本差异、图片编辑、公式模式和 ShapeSheet 安全边界。

## 0.1.0 - 2026-07-22

- 首个 Windows-only Visio Scientific Illustrator 版本。
- 通过本地 C# STA COM Bridge 在插件自有、用户可见的 Microsoft Visio 窗口中逐步绘图。
- 支持可编辑形状、文字、图片、样式、分组、容器、稳定 `User.CodexId`，以及带标签、路由、显式连接点和折线路点的真实粘附一维连接线。
- 提供局部更新、检查、撤销单元、页面适配和 2000 px 页面预览；未请求的字段不会覆盖用户手工修改。
- 支持安全保存并重新校验 VSDX，以及使用 Visio 原生接口导出 PNG、SVG 和 PDF。
- 增加绑定页激活与保存后重绑、进程所有权隔离、OLE 忙碌重试、结构化稳定错误码和严格参数校验。
- 用完整的 Visio Skill、插件元数据、隐私边界、Windows 安装器和双平台 CI 替换原有工作流。
