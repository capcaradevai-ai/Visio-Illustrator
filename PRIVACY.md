# Privacy / 隐私说明

## English

Visio Scientific Illustrator runs locally on Windows. It has no network listener, telemetry, analytics, account collection, or hosted service. The Node MCP servers and the C# Bridge communicate only through child-process standard input and output.

The plugin creates and owns a dedicated Microsoft Visio process. It does not attach to, modify, or close unrelated user-owned Visio processes. Reference files, VSDX documents, and page-export previews remain local unless the user separately uploads or shares them.

Temporary previews and validation copies are created only inside plugin-owned session directories. Cleanup is restricted to those exact files and directories. The plugin rejects macro-enabled Visio formats and does not log image contents or full Shape text.

## 中文

Visio Scientific Illustrator 仅在 Windows 本机运行，不监听网络端口，不包含遥测、统计分析、账户收集或托管服务。Node MCP 服务与 C# Bridge 仅通过父子进程的标准输入输出通信。

插件会创建并拥有一个专用 Microsoft Visio 进程，不连接、不修改、也不关闭无关的用户 Visio 进程。参考文件、VSDX 文档和页面预览默认保留在本机，除非用户另外上传或分享。

临时预览和验证副本只会建立在插件拥有的会话目录中，清理范围严格限制为这些明确文件和目录。插件拒绝启用宏的 Visio 格式，也不会记录图片内容或完整 Shape 文本。
