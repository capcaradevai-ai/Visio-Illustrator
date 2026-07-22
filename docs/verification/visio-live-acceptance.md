# Visio Live Acceptance

- Date: 2026-07-22
- Result: PASS
- Platform: Windows x64
- Microsoft Visio: 16.0.20131.20154 x64
- Node.js: 24.14.0
- .NET SDK: 8.0.204
- Plugin source version: 0.1.0
- Installed personal version: 0.1.0+codex.20260722154602

## Automated suites

| Check | Result | Evidence |
|---|---|---|
| Repository, syntax, and MCP smoke checks | PASS | Both servers initialized; exact lists contained 18 live tools and 3 file tools; unknown tools returned `isError=true`; both processes exited when stdin closed |
| Node unit tests | PASS | 20 passed, 0 failed |
| Bridge unit tests | PASS | 70 passed, 0 failed |
| Real Visio integration tests | PASS | 20 passed, 0 failed; exercised sessions, actual-page binding, shapes, glued and routed connectors, edits and undo, previews, safe save transactions, file mode, Masters, groups, and containers |
| Skill validation | PASS | `quick_validate.py` accepted the Visio Skill |
| Plugin validation | PASS | `validate_plugin.py` accepted both repository and installed copies |

## Visible live drawing

The acceptance harness started the actual published `VisioBridge.exe` through the Node MCP server and opened a visible, plugin-owned Visio window. It then performed these individually visible operations with 500 ms pauses:

1. Added editable rounded rectangle `input` with label `Input`.
2. Added editable rounded rectangle `encoder` with label `Encoder`.
3. Added one labeled 1D right-angle connector from `input` to `encoder`.

Inspection returned two 2D shapes and one connector. The connector reported exactly two semantic targets, `input` and `encoder`. A 2000 px live preview was exported and consumed successfully.

Result: PASS.

## External/manual edit preservation

While the visible plugin session remained open, an independent COM client changed `Codex_encoder` directly through Visio, bypassing the plugin update command:

- moved it to top-left pixel position `(860, 560)`;
- changed its fill to `RGB(213,232,212)`.

The plugin then inspected the shared page and issued a label-only update from `Encoder` to `Backbone`. A second inspection returned unchanged bounds `(860, 560, 320, 160)`. The exported PNG visually confirms that the externally applied green fill remained. The integration suite separately confirms that this label-only mutation is one Visio undo unit and undo restores only the label change.

Result: PASS (out-of-band COM proxy for a user edit; no OS mouse or keyboard automation was used).

## Save, validation, preview, and export

- Live save produced a reopenable VSDX and reported `validated=true`.
- Independent file mode reopened it read-only and reported 1 page, 2 shapes, and 1 connector.
- PNG export used width 2000 px and passed signature and dimension validation.
- An intentionally invalid PNG request (`page_scope=all`) returned `INVALID_ARGUMENT`; the existing PNG SHA-256 was unchanged.
- `SafeFileWriterTests.Failed_overwrite_restores_original_file` and `Busy_COM_save_restores_original_and_remains_retryable` confirmed VSDX rollback and `VISIO_BUSY` propagation after a failed overwrite.
- Preview and file-mode integration tests confirmed that Visio raster export settings are restored.

Result: PASS.

## Process ownership

A separate user-owned visible Visio process was opened first. The plugin then launched its own session:

- user-owned PID: 50068;
- plugin-owned PID: 69172.

After `shutdown(confirm=true, dirty_action=discard)`, the plugin PID exited while the user-owned PID remained alive and usable. The test subsequently closed its own user process. All later acceptance Visio and Bridge processes exited naturally; no process remained after the bounded wait.

Result: PASS.

## Local Codex installation

The plugin was installed as a personal Codex plugin using the official plugin-creator helpers:

- Marketplace: `%USERPROFILE%\.agents\plugins\marketplace.json`
- Plugin root: `%USERPROFILE%\plugins\visio-scientific-illustrator`
- Installation policy: `INSTALLED_BY_DEFAULT`
- Installed version: `0.1.0+codex.20260722154602`
- Installed Bridge: self-contained `bin\win-x64\VisioBridge.exe`, 162,167,480 bytes
- Installed Bridge SHA-256: `31517A7C92D840313C4F5BC45D18D9039DBC65C52A984C4C9FA94E7A6773CDA1`; matched the repository build
- Installed MCP verification: 18 live tools and 3 file tools

The current Codex task cannot reload newly installed plugins in place. Restart Codex and create a new task to load the Skill and both MCP servers.

Installation payload result: PASS. Post-restart UI discovery is the required user-side reload step.

## Delivered artifacts

| File | Bytes | SHA-256 |
|---|---:|---|
| [acceptance.vsdx](artifacts/acceptance.vsdx) | 18,946 | `C8FB6463544841032114E47C390898BB1A45A872025BE82D99DFF381FC360BF0` |
| [acceptance.png](artifacts/acceptance.png) | 18,618 | `5C79A0BC88F50947140F6915B075E63F8ED28FCB59F8DAF3FAB5249D24A89DF4` |

The VSDX is the editable source. The PNG is the 2000 px visual-review export.

## Release limitations

- Windows x64 and Microsoft Visio desktop only.
- Connector endpoints are truly glued. The MCP supports Visio dynamic, right-angle and straight routing, plus editable 1D polyline connectors with 1–64 explicit intermediate waypoints.
- Raster or rendered scientific content inserted as an image remains one Visio image object rather than editable internal geometry.

No unresolved implementation or acceptance failure remains. Codex restart is required only to reload the newly installed plugin into the application UI.
