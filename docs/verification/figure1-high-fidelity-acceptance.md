# Figure 1 高保真 Visio 实机验收

- 验收日期：2026-07-23
- Visio 版本：16.0
- 插件版本：0.2.0
- 最终版本：v13
- 执行方式：新建可见的 Microsoft Visio 窗口，通过 Bridge/COM 逐命令绘制，每个命令刷新后等待 500 ms
- 禁止项确认：未使用系统鼠标或键盘自动化；未预生成 VSDX 后再打开

## 输入

| 工件 | SHA-256 |
|---|---|
| `E:\code\pythoncoda\twolink\draw\fig1_recreated.drawio` | `E7CF39680D213AC995C57B62541AB5B2897BA71D3ABDB25DE69CA688C4B45B55` |
| `E:\code\pythoncoda\twolink\Feature_Enhanced_Variational_Network_for_Visual_Reconstruction_of_Human_Motion_from_WiFi\figure1_sdmore_overview.png` | `BA30B9E91BC2CAFCA4EC4B0ABB5A16880BBF07FE4685925CD3F8B47C0B2FF1C5` |
| `E:\code\pythoncoda\twolink\visio\figure1_video_frame_source_v1.png` | `7BBF76F1BAF57C5D750090A651D31F1293B040E9C4F1CBD38691CD09A9C9EF48` |

`fig2_recreated_2000px.png` 是另一张 Figure 2，不属于本次 Figure 1 验收基线，因此没有参与比较。

## 场景与可编辑性

| 指标 | 数值 |
|---|---:|
| draw.io 源 vertex / edge | 273 / 59 |
| SceneGraph node / connector | 273 / 29 |
| 局部原图视频帧 Image Shape | 5 |
| 期望语义 ID / 实际找到 | 302 / 302 |
| 递归 Inspect 图元 | 338 |
| 期望 Connector / 两端真实 GlueTo | 29 / 29 |
| 文字不匹配 | 0 |
| Fidelity 错误 | 0 |

除五个视频帧内部的照片像素外，标题、公式、框、波形、网格、张量、人体骨架、形状和连接线均为可编辑 Visio 图元。照片本身也是可移动、缩放、替换的 Visio Image Shape，但其内部像素不能作为独立矢量元素编辑。

## 可见绘制与分区检查

总计执行 576 个可见操作，每个操作后刷新 Visio 并等待 500 ms。执行顺序按逻辑区域组织，完成一个区域后立即导出 Preview、运行 Inspect 并进行局部视觉比较。

| 区域 | 完成步骤 | 图形数 | 连接线数 | MAE | Edge F1 |
|---|---:|---:|---:|---:|---:|
| 数据采集与预处理 | 203 | 100 | 14 | 0.1054 | 0.1388 |
| 静态—动态 CSI 特征分解 | 377 | 185 | 18 | 0.1293 | 0.3343 |
| 两阶段训练与推理 | 576 | 309 | 29 | 0.0893 | 0.4044 |

分区预览：

- `figure1_sdmore_visio_hifi_v13_region_1_checkpoint.png`
- `figure1_sdmore_visio_hifi_v13_region_2_checkpoint.png`
- `figure1_sdmore_visio_hifi_v13_region_3_checkpoint.png`

分区步骤数和图元数来自实际会话检查，不是事后估算。

## 最终视觉比较

最终 PNG 为 2000 × 945，参考图为 1817 × 866。比较器采用保持纵横比的 letterbox 归一化，不拉伸候选图。

| MAE | Edge precision | Edge recall | Edge F1 | Coverage ratio |
|---:|---:|---:|---:|---:|
| 0.1068 | 0.4197 | 0.4216 | 0.4206 | 0.9030 |

v11 的 Edge F1 为 0.4273，但标题和多个窄框文字严重溢出、碎行。v13 修复了 Visio `shrink_text` 的测量逻辑，视觉排版明显更完整，因此最终选择 v13。v12 因所有文本被过度缩小而被拒绝，不作为交付版本。

## 最终产物

| 工件 | 字节数 | SHA-256 |
|---|---:|---|
| `figure1_sdmore_visio_hifi_v13.vsdx` | 93,529 | `C294162612100CC862DB2634E33ADCDFCF3D827F8830EDAD44CDABB965981A46` |
| `figure1_sdmore_visio_hifi_v13_2000px.png` | 243,997 | `E07203DEC50E3CF522D3724E61002D6DA114D43B7EBF4D6ED8BACC8EE1023035` |
| `figure1_sdmore_visio_hifi_v13_scene.json` | 231,924 | `786032B25D814D66EC84F07CD3A891A38FC7B5C932E22E929D1BE61394CBFF9F` |

完整机器可读结果见 `figure1_sdmore_visio_hifi_v13_acceptance.json`，差异热图见 `figure1_sdmore_visio_hifi_v13_difference.png`。

## 本轮修复验证

1. SceneExecutor 改为区域优先执行，连接线归入两个端点中较晚完成的区域，确保检查点真正在区域完成后触发。
2. Fidelity Validator 校验连接线实际粘附的源、目标语义 ID 集合，不再只检查连接数量。
3. draw.io 富文本实体解析保留行内格式，重复 `mxCell` ID 返回稳定错误。
4. Visio 字号通过 Character ShapeSheet Cell 写入，保留小数 point 值。
5. `shrink_text` 使用 `TEXTWIDTH` 与 `TEXTHEIGHT` 分阶段测量，兼容自动文本块并遵守最小字号。
6. 视觉比较对不同纵横比进行 letterbox，不通过拉伸提高相似度。
7. 原图中的局部视频帧按确定性像素裁剪嵌入，避免生成式图像替换参考内容。

结论：v13 满足可见逐步绘制、500 ms 间隔、逻辑区域检查、可编辑 Visio 图元、连接线端点真实粘附、VSDX 保存校验和 2000 px PNG 导出要求。
