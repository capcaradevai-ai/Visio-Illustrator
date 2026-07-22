# Changelog

## 0.1.0 - 2026-07-22

- 首个 Windows-only Visio Scientific Illustrator 版本。
- 通过本地 C# STA COM Bridge 在插件自有、用户可见的 Microsoft Visio 窗口中逐步绘图。
- 支持可编辑形状、文字、图片、样式、分组、容器、稳定 `User.CodexId`，以及带标签、路由、显式连接点和折线路点的真实粘附一维连接线。
- 提供局部更新、检查、撤销单元、页面适配和 2000 px 页面预览；未请求的字段不会覆盖用户手工修改。
- 支持安全保存并重新校验 VSDX，以及使用 Visio 原生接口导出 PNG、SVG 和 PDF。
- 增加绑定页激活与保存后重绑、进程所有权隔离、OLE 忙碌重试、结构化稳定错误码和严格参数校验。
- 用完整的 Visio Skill、插件元数据、隐私边界、Windows 安装器和双平台 CI 替换原有工作流。
