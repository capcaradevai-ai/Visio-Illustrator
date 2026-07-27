# Visio Test Edition A/B 验收摘要

版本：`0.4.0-test.1`

结论：A/B 状态为 `incomplete`，Test Edition 默认质量配置保持 `strict`。

已完成的验证包括固定同一简单参考图和 SceneGraph、Strict 先于 Adaptive 的固定顺序、最多两个区域、每区最多一次纠正、工具响应字节与估算 token 统计，以及结构、胶合、可编辑性、视觉和文字语义评分字段。Bridge 仅按 UTF-8 响应字节除以四估算工具 token，并明确标记 `estimated=true`。

本次宿主任务不能向插件提供两次独立绘图运行的真实输入/输出 token，因此没有伪造模型成本数据，也没有据此把 Adaptive 设为默认。后续只有在同一模型、同一推理等级下完成 Strict 与 Adaptive 两个 finalized run，并同时满足实施计划中的成本和质量阈值，才允许把默认配置改为 `adaptive`。

当前推荐：

- 投稿级或参考图敏感任务使用 `strict`。
- 手工测试成本收益时显式选择 `adaptive`。
- 草稿和低成本批处理可显式选择 `economy`。
