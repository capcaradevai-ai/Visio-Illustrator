# Test Edition Phase 5：双平台安装、A/B 验收与预发布实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` and `requesting-code-review`. Complete Phases 1–4 first.

**Goal:** 为 Codex 和 Claude Code 提供与稳定版并存的 Test Edition 安装、更新、验证和使用文档，用一张简单图验证 Adaptive 的成本收益，再完成 `v0.4.0-test.1` 预发布门。

**Architecture:** 根目录两个薄安装器调用 edition 内的验证逻辑。安装器只操作 Test marketplace/plugin ID，不卸载、不覆盖稳定版。A/B runner 记录工具统计和人工/模型审查结果，但不把模型调用嵌入 Bridge。

**Tech Stack:** PowerShell、Codex CLI、Claude Code CLI、Node.js、JSON、Git。

**Path Key:** 下文 `TEST_PLUGIN_ROOT` 严格表示仓库相对路径 `editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test`。

---

## Task 1：完成 edition 仓库校验和 smoke

**Files:**

- Modify: `editions/visio-scientific-illustrator-test/scripts/validate-repo.mjs`
- Modify: `editions/visio-scientific-illustrator-test/scripts/smoke-test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/dual-platform-packaging.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/utf8-content.test.mjs`

- [ ] **Step 1：写失败测试**

校验：

- package、Codex manifest、Claude manifest 都为 `0.4.0-test.1`；
-两个 Marketplace 都名为 `visio-scientific-tools-test`；
-两个 MCP 配置分别含 `visio-test-live`、`visio-test-file-utils`；
- Claude MCP 使用 `${CLAUDE_PLUGIN_ROOT}/scripts/...` 且不声明 `cwd`；
- Codex MCP 使用 edition 内相对路径；
-五个 Skill 都被插件发现；
- Test Bridge 存在且版本匹配；
- stable hash 通过；
-新文本文件能按 UTF-8 严格解码且不含 U+FFFD。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/dual-platform-packaging.test.mjs editions/visio-scientific-illustrator-test/tests/node/utf8-content.test.mjs
```

- [ ] **Step 3：实现校验**

`validate-repo.mjs` 失败时打印具体相对路径和合同字段，不输出本机用户名、完整用户目录或凭据样式字符串。`smoke-test.mjs` 使用 fake bridge 检查 MCP initialize、tools/list、一个 capability 摘要和一个非法图片声明。

- [ ] **Step 4：运行**

```powershell
npm --prefix editions/visio-scientific-illustrator-test run check
```

Expected:

```text
Test Edition repository structure and portability checks passed.
Test Edition MCP smoke tests passed.
Stable protected files unchanged.
```

- [ ] **Step 5：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test
git commit -m "test(test-edition): validate isolated dual-platform package"
```

## Task 2：实现 Codex Test 安装器

**Files:**

- Create: `install-test.ps1`
- Create: `editions/visio-scientific-illustrator-test/tests/node/codex-test-installer.test.mjs`

- [ ] **Step 1：写静态安全测试**

安装器必须：

- 解析当前仓库绝对路径；
-先运行 stable hash、edition check 和 Bridge 文件存在检查；
-只注册 `visio-scientific-tools-test`；
-只安装/更新 `visio-scientific-illustrator-test@visio-scientific-tools-test`；
-幂等；
-安装后读取 Codex 插件列表验证 Stable 和 Test 都已启用；
-不调用卸载稳定版的命令；
-不写 `C:\Users\<name>` 硬编码路径；
-失败立即退出非零。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/codex-test-installer.test.mjs
```

- [ ] **Step 3：实现安装器**

输出必须明确区分：

```text
Stable plugin: visio-scientific-illustrator@visio-scientific-tools
Test plugin: visio-scientific-illustrator-test@visio-scientific-tools-test
```

如果稳定版未安装，仅报告状态，不自动安装稳定版；Test Edition 安装仍可继续。安装器不修改 Codex 默认模型或推理等级。

- [ ] **Step 4：运行静态测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/codex-test-installer.test.mjs
```

- [ ] **Step 5：提交**

```powershell
git add -- install-test.ps1 editions/visio-scientific-illustrator-test/tests/node/codex-test-installer.test.mjs
git commit -m "feat(test-edition): add codex test installer"
```

## Task 3：实现 Claude Code Test 安装器

**Files:**

- Create: `install-claude-test.ps1`
- Create: `editions/visio-scientific-illustrator-test/tests/node/claude-test-installer.test.mjs`

- [ ] **Step 1：写静态安全测试**

安装器只操作 Test marketplace/plugin ID，支持首次安装和更新，安装后用 Claude 插件列表验证 Test 已启用，并提示 `/reload-plugins` 或新建会话。不得删除 Claude 稳定版缓存或配置。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/claude-test-installer.test.mjs
```

- [ ] **Step 3：实现和验证**

Claude 显式入口：

```text
/visio-scientific-illustrator-test:visio-test-orchestrate-scientific-figure
```

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/claude-test-installer.test.mjs
```

- [ ] **Step 4：提交**

```powershell
git add -- install-claude-test.ps1 editions/visio-scientific-illustrator-test/tests/node/claude-test-installer.test.mjs
git commit -m "feat(test-edition): add claude test installer"
```

## Task 4：编写中英文安装和使用 README

**Files:**

- Create: `README.test.md`
- Create: `README.test.en.md`
- Create: `editions/visio-scientific-illustrator-test/tests/node/readme-test-edition.test.mjs`

- [ ] **Step 1：写文档合同测试**

两个 README 都必须包含：

- Stable 与 Test 区别；
-系统要求：Windows、桌面版 Microsoft Visio、Node.js 22；
-Codex 安装、更新、验证、选择 Test；
-Claude Code 安装、更新、验证、显式命令；
-参考图复刻提示词；
- strict/adaptive/economy 选择；
-原生图元、复合组件和原子图片的可编辑边界；
-默认 500 ms 可见绘制；
-禁止鼠标键盘自动化与先生成再打开；
-保存 VSDX、校验和导出 PNG；
-常见错误码；
-卸载 Test 不影响 Stable；
-隐私和本地文件边界。

- [ ] **Step 2：运行失败测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/readme-test-edition.test.mjs
```

- [ ] **Step 3：写中文 README**

推荐提示词必须使用 Test 名称：

```text
使用 Visio Scientific Illustrator Test，以 adaptive 模式在可见的 Microsoft Visio
页面中逐步重绘这张参考图。默认每步间隔 500 ms；所有可重建文字、形状、表格、
图表和连接线保持可编辑，连接线端点真正胶合。每个区域通过当前 revision 的审查后
再继续；完成后保存 VSDX、校验文件并导出宽度 2000 px 的 PNG。
```

- [ ] **Step 4：写英文 README 并运行测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/readme-test-edition.test.mjs
```

- [ ] **Step 5：提交**

```powershell
git add -- README.test.md README.test.en.md editions/visio-scientific-illustrator-test/tests/node/readme-test-edition.test.mjs
git commit -m "docs(test-edition): add bilingual install and usage guides"
```

## Task 5：实现轻量统计和 A/B runner

**Files:**

- Create: `TEST_PLUGIN_ROOT/scripts/telemetry.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/live-server.mjs`
- Modify: `TEST_PLUGIN_ROOT/scripts/tool-schemas.mjs`
- Create: `editions/visio-scientific-illustrator-test/scripts/run-quality-ab.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/telemetry.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/node/quality-ab-runner.test.mjs`
- Create: `editions/visio-scientific-illustrator-test/tests/fixtures/simple-quality-loop/ab-config.json`

- [ ] **Step 1：写失败测试**

本地统计字段：

```text
run_id
profile
model_name
reasoning_level
started_at
elapsed_ms
mcp_call_count
tool_response_bytes
estimated_tool_tokens
preview_context_count
model_region_review_count
model_full_page_review_count
safe_patch_count
finding_counts
structure_score
glue_score
editability_score
visual_score
text_semantic_accuracy
```

Bridge 不猜测宿主模型 token。`estimated_tool_tokens` 仅按 UTF-8 响应字节/4 的固定估算，并标记 `estimated=true`。真实输入/输出 token 由执行者从宿主任务统计填入 A/B 报告；缺少真实 token 时 A/B 结果为 `incomplete`，不能据此把 Adaptive 设为默认。

- [ ] **Step 2：实现本地统计**

不记录参考图字节、图片 base64、完整文本、完整文件路径或用户 prompt。路径只记录扩展名和不可逆 hash。统计文件写到 Test 会话目录。

新增紧凑工具：

```text
visio_test_live_get_run_metrics
visio_test_live_finalize_run_metrics
```

`get` 返回工具可测字段和本地临时路径；`finalize` 只接受宿主已知的模型名、推理等级、真实输入/输出 token、最终模型视觉分和文字语义准确率，并写出不可再修改的 run JSON。finalize 不调用模型。

- [ ] **Step 3：实现 A/B harness**

`run-quality-ab.mjs` 只负责准备和比较，不直接调用 Codex、Claude 或任何模型 API：

```powershell
node editions/visio-scientific-illustrator-test/scripts/run-quality-ab.mjs prepare --config editions/visio-scientific-illustrator-test/tests/fixtures/simple-quality-loop/ab-config.json
node editions/visio-scientific-illustrator-test/scripts/run-quality-ab.mjs compare --manifest editions/visio-scientific-illustrator-test/TestResults/quality-ab/manifest.json
```

`prepare` 固定同一 reference、scene、机器标识和顺序，生成 Strict/Adaptive 两个 run descriptor；本次发布固定先 Strict 后 Adaptive，不额外重复。实际模型审查由执行计划的 Codex/Claude 会话通过 Orchestrator Skill 完成。`compare` 只接受两个已经 finalize 的 run JSON。

输出：

```text
editions/visio-scientific-illustrator-test/TestResults/quality-ab/manifest.json
editions/visio-scientific-illustrator-test/TestResults/quality-ab/summary.json
editions/visio-scientific-illustrator-test/TestResults/quality-ab/report.md
```

`TestResults/` 不提交，只提交 `ab-config.json` 和 runner。

- [ ] **Step 4：运行单元测试**

```powershell
node --test editions/visio-scientific-illustrator-test/tests/node/telemetry.test.mjs editions/visio-scientific-illustrator-test/tests/node/quality-ab-runner.test.mjs
```

- [ ] **Step 5：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/scripts editions/visio-scientific-illustrator-test/scripts/run-quality-ab.mjs editions/visio-scientific-illustrator-test/tests
git commit -m "feat(test-edition): measure quality profile cost"
```

## Task 6：执行简单 Strict/Adaptive A/B

- [ ] **Step 1：前置检查**

```powershell
npm --prefix editions/visio-scientific-illustrator-test test
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
powershell -ExecutionPolicy Bypass -File .\install-test.ps1
```

安装后必须让 Codex 重新加载插件；若当前任务不能动态发现新 MCP 和五个 Test Skill，则在同一分支新建 Codex 任务，并从本 Task 的 Step 2 继续。该交接不修改代码，A/B manifest 和所有测试结果仍保存在 edition 的 `TestResults/quality-ab/`。

- [ ] **Step 2：执行两次简单图**

先准备两个 run：

```powershell
node editions/visio-scientific-illustrator-test/scripts/run-quality-ab.mjs prepare --config editions/visio-scientific-illustrator-test/tests/fixtures/simple-quality-loop/ab-config.json
```

要求：

- 同一张 `800 × 500` 简单图；
-每次最多两个区域；
-每次最多一次纠正；
- Strict 每区模型审查；
- Adaptive 按策略审查；
-两者各一次最终全图模型审查；
-不得换成复杂论文图。

- [ ] **Step 3：由宿主依次执行并 finalize 两个 run**

执行者必须在同一模型和推理等级下：

1. 显式选择 Test Edition Orchestrator 和 Strict descriptor；
2. 完成后调用 `visio_test_live_finalize_run_metrics`，写入真实宿主 token 和最终 Reviewer 分数；
3. 关闭该 Test 自有 Visio 会话；
4. 用 Adaptive descriptor 重复一次；
5. 不在两次 run 之间修改 fixture、profile 阈值或工具代码。

- [ ] **Step 4：比较并判定默认配置**

```powershell
node editions/visio-scientific-illustrator-test/scripts/run-quality-ab.mjs compare --manifest editions/visio-scientific-illustrator-test/TestResults/quality-ab/manifest.json
```

全部满足时 `adaptive` 保持默认：

```text
model image reviews reduction >= 50%
tool response token reduction >= 40%
elapsed time reduction >= 30%
structure/glue/editability not lower
adaptive visual score >= strict visual score * 0.90
text and semantic accuracy = 1.00
```

任一未满足时，只修改 Test Edition 的 `quality-profiles.mjs` 和 README，将默认改为 `strict`；不得放宽阈值或修改测量结果。

- [ ] **Step 5：保存不含敏感数据的验收摘要**

Create:

```text
docs/superpowers/reports/2026-07-25-visio-test-edition-ab-summary.md
```

摘要只记录模型名、推理等级、计数、分数、耗时、默认策略结论和报告 SHA-256，不提交参考图、VSDX、PNG 或完整日志。

- [ ] **Step 6：提交**

```powershell
git add -- editions/visio-scientific-illustrator-test/plugins/visio-scientific-illustrator-test/scripts/quality-profiles.mjs README.test.md README.test.en.md docs/superpowers/reports/2026-07-25-visio-test-edition-ab-summary.md
git commit -m "test(test-edition): verify adaptive quality economics"
```

## Task 7：本机双平台安装与并存验证

- [ ] **Step 1：安装 Codex Test**

```powershell
powershell -ExecutionPolicy Bypass -File .\install-test.ps1
```

Expected: Codex 同时列出 Stable 与 Test，且 Test MCP 为 `visio-test-live` / `visio-test-file-utils`。

- [ ] **Step 2：安装 Claude Code Test**

```powershell
powershell -ExecutionPolicy Bypass -File .\install-claude-test.ps1
```

Expected: Claude 同时列出 Stable 与 Test。执行 `/reload-plugins` 或新建会话后，显式 Test 命令可发现。

- [ ] **Step 3：进程隔离 smoke**

分别从 Stable 与 Test 启动一个简单空白可见会话，验证：

-不同 Bridge exe；
-不同 PID；
-不同临时根目录；
-关闭 Test 不影响 Stable；
-关闭 Stable 不影响 Test。

完成后按各自确认参数正常关闭两个插件拥有的 Visio 进程。

## Task 8：完整发布检查和代码审查

- [ ] **Step 1：完整自动测试**

```powershell
npm test
npm --prefix editions/visio-scientific-illustrator-test test
node editions/visio-scientific-illustrator-test/scripts/run-simple-quality-loop.mjs --step-delay-ms 50
node editions/visio-scientific-illustrator-test/scripts/verify-stable-hashes.mjs
git diff --check
```

- [ ] **Step 2：发布内容扫描**

```powershell
Get-ChildItem -LiteralPath editions/visio-scientific-illustrator-test,README.test.md,README.test.en.md,install-test.ps1,install-claude-test.ps1 -Recurse -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' } |
  Select-String -Pattern 'TODO|TBD|PLACEHOLDER|github_pat_|gho_|C:\\Users\\[^\\]+|�'
```

Expected: 无匹配。

- [ ] **Step 3：独立代码审查**

使用 `requesting-code-review`，审查范围从设计规格提交 `8c5d4cf` 到当前 HEAD。必须检查：

-稳定版保护；
-进程与 COM 所有权；
-revision 原子性；
-stale evidence；
-图片声明；
-复合组件可编辑性；
-工具输出成本；
-Codex/Claude 安装冲突；
-测试是否真的覆盖可见实时绘制。

Critical 或 Important finding 必须修复并重新运行相关测试；不得只记录后发布。

- [ ] **Step 4：提交审查修复**

审查没有产生文件变化时不创建空提交。若产生修复，暂存修复文件和对应新增测试，提交为 `fix(test-edition): resolve prerelease review findings`，然后重新执行 Task 8 Step 1–3。

## Task 9：推送与预发布标签

仅在用户再次明确批准发布后执行：

```powershell
git status --short
git push -u origin codex/visio-test-edition
git tag -a v0.4.0-test.1 -m "Visio Scientific Illustrator Test 0.4.0-test.1"
git push origin v0.4.0-test.1
```

发布前最终断言：

- [ ] `git status --short` 仅显示未跟踪 `.neuralmemory/`，没有已跟踪修改。
- [ ] stable hash 通过。
- [ ] Codex Stable/Test 均启用。
- [ ] Claude Stable/Test 均启用。
- [ ] A/B 默认策略结论已写入 README。
- [ ] 无 Critical 或 Important code review finding。
