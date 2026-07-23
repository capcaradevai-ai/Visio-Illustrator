# Figure 1 高保真 Visio 实机验收

- 验收日期：2026-07-23
- Visio 版本：16.0
- 插件版本：0.2.0
- 验收模式：新建可见 Visio 窗口，逐命令实时绘制，每个命令后刷新并等待 500 ms
- 输入方式：Bridge/COM；未使用系统鼠标或键盘自动化，未预生成 VSDX 后再打开

## 输入与可复现标识

| 工件 | 字节数 | SHA-256 |
|---|---:|---|
| `E:\code\pythoncoda\twolink\draw\fig1_recreated.drawio` | 90,539 | `E7CF39680D213AC995C57B62541AB5B2897BA71D3ABDB25DE69CA688C4B45B55` |
| `E:\code\pythoncoda\twolink\Feature_Enhanced_Variational_Network_for_Visual_Reconstruction_of_Human_Motion_from_WiFi\figure1_sdmore_overview.png` | 1,438,704 | `BA30B9E91BC2CAFCA4EC4B0ABB5A16880BBF07FE4685925CD3F8B47C0B2FF1C5` |
| draw.io 蓝图预览 `fig1_recreated_2000px.png` | 590,432 | `2AF956B4D0ACD90BCDC4E9A3D4C4E97167B1780D51D714E656F26767EBA1FC29` |

参考 PNG 为 1817 × 866；draw.io 文件是绘制蓝图，不包含嵌入位图。

## 编译结果

| 指标 | 数值 |
|---|---:|
| draw.io vertex | 273 |
| draw.io edge | 59 |
| SceneGraph node | 303 |
| 语义 Connector | 29 |
| 装饰性可编辑 Path | 30 |
| Gradient | 3 |
| 旋转图元 | 24 |
| HTML/RichText 标签 | 316 |
| 保留的显式 waypoint | 10 |
| 编译警告 | 0 |

无线链路和雪花由可编辑科学组件重建。波形、人体、骨架、网格、张量、潜变量条、框线和文字均为 Visio 图元，没有用整图位图伪装可编辑结果。

## 可见绘制与区域检查

共执行 636 个可见操作。每次操作后调用 Visio 刷新并等待 500 ms；三个主区域完成后分别导出 Preview、运行局部比较并 Inspect。

| 区域 | Bounds | Inspect 图元数 | MAE | Edge F1 | Coverage ratio |
|---|---|---:|---:|---:|---:|
| 数据采集与预处理 | 20,20,690,820 | 353 | 0.1200 | 0.4576 | 0.7290 |
| 静态/动态 CSI 分解 | 720,20,400,820 | 357 | 0.1170 | 0.3728 | 0.9437 |
| 两阶段训练与推理 | 1130,20,670,820 | 368 | 0.0901 | 0.4155 | 1.1279 |

区域预览文件：

- `figure1_sdmore_visio_hifi_v10_region_1_checkpoint.png`
- `figure1_sdmore_visio_hifi_v10_region_2_checkpoint.png`
- `figure1_sdmore_visio_hifi_v10_region_3_checkpoint.png`

## 结构门禁

| 门禁 | 结果 |
|---|---:|
| VSDX 可打开且结构有效 | PASS |
| Page | 1 |
| Scene 期望语义 ID / 实际找到 | 332 / 332 |
| Scene 期望图元 / Inspect 实际图元 | 332 / 368 |
| 期望 Connector / 两端真实 GlueTo | 29 / 29 |
| 文字不匹配 | 0 |
| Fidelity 错误 | 0 |
| Fidelity 警告 | 0 |

文件级 `shapeCount=339` 统计顶层 VSDX Shape；递归 Inspect 和 Fidelity Validator 统计组件子图元，得到 368。两种计数口径不同但均通过各自门禁。

## 视觉比较

最终 Visio Preview 为 1820 × 860。比较器保留原始画布差异记录，并将候选图双线性归一化到参考尺寸后计算结构指标。

| 比较对象 | MAE | Edge precision | Edge recall | Edge F1 | Coverage ratio |
|---|---:|---:|---:|---:|---:|
| 原始论文参考 PNG | 0.1107 | 0.4020 | 0.4102 | 0.4061 | 0.9319 |
| 可编辑 draw.io 蓝图预览 | 0.0788 | 0.7000 | 0.7216 | 0.7106 | 1.0618 |

原始论文参考图包含与可编辑 draw.io 蓝图不同的像素细节和排版，因此原图指标仅作为诊断，不单独作为通过条件。对实际迁移蓝图的 Edge F1 为 0.7106，且结构门禁证明结果不是靠大面积白底或整图位图取得。

## 最终产物

| 工件 | 尺寸/字节数 | SHA-256 |
|---|---:|---|
| `E:\code\pythoncoda\twolink\visio\figure1_sdmore_visio_hifi_v10.vsdx` | 75,369 bytes | `1637B0D2BEC687F1E078FA5DDB9E5112C9AF9A03A4AE2FF986BBDB8172844D23` |
| `E:\code\pythoncoda\twolink\visio\figure1_sdmore_visio_hifi_v10_2000px.png` | 2000 × 945 / 160,176 bytes | `5DC6269F4E973A664A603B5C3377ED59AF404CB254EA973F8A03969538E9D884` |
| `E:\code\pythoncoda\twolink\visio\figure1_sdmore_visio_hifi_v10_scene.json` | 249,713 bytes | `A63319C396A1943A14BE73D6C53479FE969EAA5D80695AF8472515AB7C04293E` |
| `E:\code\pythoncoda\twolink\visio\figure1_sdmore_visio_hifi_v10_acceptance.json` | 7,139 bytes | `6479EBE4D288BCA73A2EF208076C41B4336013114584F1F0BFEFDD4E4E05349A` |

## 实机验收中发现并修复的问题

1. 大场景每次按语义 ID 递归扫描全页，形成 O(n²) COM 调用并在第 554 步超时。现改为会话级语义索引和 `NameU` 直接解析，并在删除、清空、解组、撤销和失败回滚后重建索引。
2. Bridge 只固定了标准输出 UTF-8，标准输入仍按系统代码页读取，导致 `–`、`∈`、`ℂ`、`×` 和上标乱码。现同时固定输入/输出 UTF-8。
3. draw.io `fontSize` 的像素值被当作 point，导致文字约放大 33%。现按 96 dpi 将 px 转为 pt。
4. `&vellip;` 不在 .NET 默认 HTML 解码集合中，曾显示成竖排实体文本。现显式转换为 `⋮`。
5. Visio 自动正交路由会把短链路重排为跨面板大回路。现对相邻节点使用直线，对显式 waypoint 保留固定折线，并从首末控制点推导归一化 GlueTo 端点。
6. Visio Preview 的像素尺寸会受页面单位取整影响。比较器现在记录原始尺寸和纵横比误差，再做确定性的双线性尺寸归一化；没有放宽边缘或颜色指标。

## 已知剩余差异

- Visio 与 Chromium/draw.io 的字体度量不同，少量窄框文字换行位置不完全一致。
- 原始参考 PNG 的视频帧像素内容没有出现在 draw.io 蓝图中；本次按“全部可编辑”要求保留矢量人体和帧堆叠，没有嵌入局部照片。
- Visio 导出的画布纵横比与参考 PNG 相差约 0.864%，比较器已显式记录并归一化。

结论：本次验收满足可见逐步绘制、500 ms 间隔、可编辑图元、真实粘附、分区预览检查、版本化 VSDX 保存、结构校验和 2000 px PNG 导出要求。
