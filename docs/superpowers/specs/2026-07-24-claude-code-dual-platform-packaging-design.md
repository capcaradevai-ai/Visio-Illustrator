# Visio Scientific Illustrator 双平台插件打包设计

## 目标

在不复制 Visio Bridge、MCP 脚本或 Skill 源码的前提下，让同一个
`visio-scientific-illustrator` 插件目录同时支持 Codex 与 Claude Code。
两个平台共享版本号、功能和测试基线，但使用各自原生的 Marketplace、
插件清单和 MCP 路径解析方式。

本次发布版本统一升级为 `0.3.0`。发布完成后，用户可以从
`TZ-mx/Visio-Illustrator` 安装任一平台版本，也可以在同一台 Windows
计算机上同时安装两个版本。

## 设计原则

1. 单一事实来源：Skill、Node.js MCP 服务、C# COM Bridge、Master 模板和资源只保留一份。
2. 原生兼容：Codex 与 Claude Code 分别使用官方支持的清单和安装命令。
3. 路径可迁移：任何 MCP 配置都不能依赖开发者本机的绝对路径。
4. 安装隔离：安装一个平台时不得删除或覆盖另一个平台的注册状态。
5. 版本一致：根包、两个插件清单、CHANGELOG 和安装校验使用同一版本。
6. 可验证：静态清单检查、MCP 冒烟测试、Claude 官方验证器和实际安装状态都必须通过。

## 仓库结构

保留现有 Codex 文件，并在同一目录增加 Claude Code 文件：

```text
Visio-Illustrator/
├─ .agents/plugins/marketplace.json
├─ .claude-plugin/marketplace.json
├─ install.ps1
├─ install-claude.ps1
├─ plugins/visio-scientific-illustrator/
│  ├─ .codex-plugin/plugin.json
│  ├─ .claude-plugin/plugin.json
│  ├─ .mcp.json
│  ├─ .mcp.claude.json
│  ├─ skills/
│  ├─ scripts/
│  ├─ bin/win-x64/VisioBridge.exe
│  └─ assets/
├─ README.md
└─ README.en.md
```

`.mcp.json` 继续服务 Codex，保持相对脚本路径和工作目录语义。
Claude 清单显式引用 `.mcp.claude.json`，其中使用
`${CLAUDE_PLUGIN_ROOT}` 定位 MCP 脚本。这样 Claude 将插件复制到版本化缓存后，
Node.js 仍能从正确目录启动，而 Codex 现有配置不受影响。

## 双平台清单

### Codex

现有 `.agents/plugins/marketplace.json` 与
`.codex-plugin/plugin.json` 保持结构不变，只把版本升级为 `0.3.0`。
Codex 插件继续从 `.mcp.json` 加载 `visio-live` 和
`visio-file-utils`。

### Claude Code

仓库根目录新增 `.claude-plugin/marketplace.json`，市场名称使用
`visio-scientific-tools`，插件源指向
`./plugins/visio-scientific-illustrator`。

插件目录新增 `.claude-plugin/plugin.json`，声明：

- 名称 `visio-scientific-illustrator`；
- 版本 `0.3.0`；
- `skills/` 为 Skill 根目录；
- `.mcp.claude.json` 为 MCP 服务配置；
- Windows、Microsoft Visio 和 Node.js 22+ 为运行条件；
- 项目仓库、许可证和作者信息。

Claude Code 不加载 Codex 的 `agents/openai.yaml` 作为平台元数据，但这些文件可以
保留在 Skill 目录中，不影响 Claude 对 `SKILL.md` 的发现。

## 安装流程

### Codex

`install.ps1` 继续只负责 Codex：

1. 检查 Windows、Node.js、Visio COM、Bridge 和 Codex CLI。
2. 注册 `.agents` Marketplace。
3. 安装 `visio-scientific-illustrator@visio-scientific-tools`。
4. 验证已安装版本与 Codex 清单一致。

### Claude Code

新增 `install-claude.ps1`：

1. 检查 Windows、Node.js 22+、Visio COM、Bridge 和 Claude CLI。
2. 运行 `claude plugin validate` 校验仓库 Marketplace 和插件目录。
3. 检查是否已经注册同名 Marketplace。
4. 如果同名 Marketplace 指向旧快照，先卸载该市场中的插件，再移除旧市场注册。
5. 从当前仓库根目录注册 Claude Marketplace。
6. 以用户作用域安装
   `visio-scientific-illustrator@visio-scientific-tools`。
7. 读取 `claude plugin list --json` 和 `claude plugin details`，
   验证版本、启用状态、Skill 数量和两个 MCP 服务。

安装器只处理 Claude Code 的插件状态，不调用 Codex 命令，也不删除 Codex 缓存。

## 运行数据流

两个平台最终调用相同的后端：

```text
Codex / Claude Code
        ↓
平台专属 Manifest 与 MCP 配置
        ↓
共享 live-server.mjs / file-server.mjs
        ↓
共享 VisioBridge.exe
        ↓
插件拥有的可见 Microsoft Visio 进程
```

Skill 中的 Visio 专属约束、SceneGraph、区域检查点、真实胶合、预览比较和 VSDX
校验保持一致。平台差异只存在于插件发现、路径解析和安装命令。

## 文档设计

`README.md` 作为中文主文档，`README.en.md` 作为英文文档。两个版本都按以下顺序组织：

1. 项目用途和主要能力；
2. Codex 快速安装；
3. Claude Code 快速安装；
4. 两个平台的手动、可审计安装；
5. 插件选择与调用方式；
6. 有参考图、无参考图、检查现有 VSDX 的完整提示词；
7. 更新和卸载；
8. 环境要求、权限、安全边界和故障排查；
9. 开发、构建和双平台验证命令。

所有命令使用仓库真实名称和插件 ID，不保留占位符。README 明确说明 Visio 实时
COM 控制只支持 Windows 桌面版 Microsoft Visio。

## 错误处理

- 找不到 Claude CLI：立即停止，不修改 Marketplace。
- Claude 版本不支持插件命令：报告实际版本并停止。
- Marketplace 清单或插件清单校验失败：禁止安装。
- 同名 Marketplace 来源不明确：停止并报告，不删除未知来源。
- Node.js、Visio COM 或 Bridge 缺失：安装前失败。
- 插件安装后版本不一致、Skill 缺失或 MCP 服务缺失：安装器返回失败。
- Codex 与 Claude 中任一平台失败时，不回滚另一个平台的有效安装。

安装脚本不包含静默 fallback，也不以复制 Skill 到全局目录的方式绕过插件系统。

## 测试与验收

实施采用测试先行：

1. 先扩展仓库验证测试，使其因缺少 Claude Marketplace、Manifest 和 MCP 配置而失败。
2. 添加双清单后使静态测试通过。
3. 使用 `claude plugin validate` 校验仓库根目录和插件目录。
4. 运行现有 Node.js、MCP 冒烟和 .NET 单元测试，防止 Codex/Bridge 回归。
5. 执行 `install-claude.ps1` 完成本机真实安装。
6. 验证 Claude 插件列表、详情、版本、Skill 和 MCP 服务。
7. 重新运行 Codex 插件校验，确认 Codex 版本仍可安装和启用。

发布验收条件：

- Codex 与 Claude Code 均报告版本 `0.3.0`；
- 两个平台均发现同一个 Visio Skill；
- 两个平台均发现 `visio-live` 和 `visio-file-utils`；
- MCP 冒烟测试、Node.js 测试和非 Visio 集成 .NET 测试全部通过；
- README 中的安装命令与本机验证过的命令一致；
- Git 工作区只包含本次双平台发布相关文件；
- GitHub `main` 分支包含提交和 `v0.3.0` 标签。

## 发布与恢复

实施完成后创建一个发布提交并推送到
`https://github.com/TZ-mx/Visio-Illustrator` 的 `main` 分支，同时推送
`v0.3.0` 标签。若 Claude 安装验证失败，只移除本次注册的 Claude 插件和
Marketplace；现有 Codex 插件、用户 VSDX 文件和 Visio 设置保持不变。
