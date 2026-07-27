# Visio Scientific Illustrator Test

[中文](README.test.md) | [Stable edition](README.en.md)

Test Edition is an isolated quality-focused edition that can coexist with Stable. It controls only a plugin-owned, visible Microsoft Visio window through native COM and progressively creates editable Visio objects. It adds dynamic capability discovery, five role skills, page revisions, regional quality gates, deterministic audits, atomic-raster declarations, and editable tables, charts, matrices, and grids.

Current version: `0.4.0-test.1`. The default profile is `strict`. `adaptive` will not become the default until a complete A/B run includes real host token counts.

## Requirements

- Windows 10/11 x64
- Microsoft Visio desktop with registered COM automation
- Node.js 22 or newer
- .NET 8 SDK to build the isolated Bridge from source; the published Bridge is self-contained
- Codex desktop/CLI, or Claude Code 2.1.218 or newer

Stable and Test use separate marketplace names, plugin IDs, MCP names, Bridge executables, processes, and temporary roots:

| Item | Stable | Test |
|---|---|---|
| Marketplace | `visio-scientific-tools` | `visio-scientific-tools-test` |
| Plugin | `visio-scientific-illustrator` | `visio-scientific-illustrator-test` |
| Live MCP | `visio-live` | `visio-test-live` |
| File MCP | `visio-file-utils` | `visio-test-file-utils` |

Updating or uninstalling Test must not modify Stable.

## Install or update

Build the isolated Bridge from the repository root:

```powershell
npm --prefix .\editions\visio-scientific-illustrator-test run build:bridge
```

Codex:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-test.ps1
```

Claude Code:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-claude-test.ps1
```

Both installers are idempotent update entry points. They validate Stable protection, Test manifests, Node, Visio, and the Bridge before registering only the Test IDs. Restart Codex after installation. In Claude Code, run `/reload-plugins` or start a new session.

Verify with:

```powershell
codex plugin list --json
claude plugin list --json
```

The explicit Claude Code command is:

```text
/visio-scientific-illustrator-test:visio-test-orchestrate-scientific-figure
```

## Recommended prompt

```text
Use Visio Scientific Illustrator Test in strict mode to progressively recreate this
reference in a visible Microsoft Visio page. Use a 500 ms step delay. Keep every
reconstructable label, shape, table, chart, and connector editable, and glue both
connector endpoints. Do not advance until each region passes the audit for the current
revision. Finally save and validate the VSDX and export a 2000 px wide PNG.
```

Call `visio_test_live_get_capabilities` first. Request paged capability details only for the Masters, components, or export limitations needed by the task. Each region checkpoint exports a current preview and pauses. The Bridge blocks the next region until the current revision passes. The audit tool creates its own preview hash and structure hash from the current page, preventing stale screenshots from granting a pass.

Use a PNG reference for deterministic visual auditing. JPEG references remain supported for asset preparation and extraction. If the visible Visio page is edited outside the plugin after a pass, the Bridge detects the changed structure, increments the page revision, invalidates the old evidence, and closes the region gate until a fresh audit passes.

## Profiles

- `strict`: Designer → Drawer → Reviewer → Corrector for every region; use for reference-sensitive and publication-grade figures.
- `adaptive`: calibrates the first region, reviews the most complex region, escalates anomalies, and performs a final full-page review. Expected to reduce model image reviews, but the real-token A/B run is incomplete.
- `economy`: relies primarily on deterministic structural checks and asks the model only about uncertain regions; intended for drafts and low-cost batches.

The five skills separate orchestration, design, Visio drawing, review, and correction. The model handles meaning and subjective visual judgment. Tools handle capability discovery, structure, glue, revisions, finding IDs, image declarations, and file validation.

## Editability boundary

Text, basic shapes, paths, connectors, table cells, chart axes/ticks/data marks, matrices, and grids remain independent Visio primitives. When Visio lacks one native object, the plugin composes editable primitives instead of substituting a screenshot.

Only a small irreducible visual unit may be imported as an image. It must declare `raster_reason`, `source_is_tightly_cropped=true`, `atomic_raster_unit=true`, `contains_reconstructable_content=false`, and `decomposition_note`. The declaration is persisted to both User Cells and Shape Data so it remains auditable after reopening the VSDX. Images may not contain reconstructable text, arrows, tables, axes, or legends.

Common errors include `QUALITY_GATE_BLOCKED`, `STALE_AUDIT_EVIDENCE`, `REVISION_CONFLICT`, `RASTER_DECLARATION_INVALID`, `COMPONENT_CONTRACT_INVALID`, and `CAPABILITY_NOT_AVAILABLE`.

The plugin never uses operating-system mouse or keyboard automation and does not replace live drawing with a hidden VSDX-first workflow. References, previews, VSDX files, and metrics remain local. Lightweight metrics never record image bytes, full prompts, or complete file paths.
