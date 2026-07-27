# Test Edition Tool Workflows

Use the shortest legal tool sequence and return compact handles. All public tool names begin with `visio_test_`.

## Capability discovery

1. Call `visio_test_live_get_capabilities` once per session.
2. Call `visio_test_live_get_capability_details` only for features selected by the Designer.
3. Treat unsupported requested features as explicit capability errors; do not substitute unrelated geometry.

## Reference preparation

```text
visio_test_prepare_reference
visio_test_get_reference_manifest
visio_test_get_reference_region
visio_test_extract_reference_asset
```

Load the manifest once. Request one regional crop or asset record only when the active role needs it. PNG and JPEG can be prepared for asset extraction; the deterministic pixel audit currently requires a PNG source so its decoder and score remain explicit and reproducible.

## Visible construction

1. Launch a Test Edition-owned visible document with `visio_test_live_launch`.
2. Verify ownership and state with `visio_test_live_status`.
3. Draw an authorized region with `visio_test_live_draw_scene` or a small targeted sequence.
4. Use native Visio shapes, editable paths, rich text, groups, layers, components, and 1D connectors.
5. Refresh and export the coherent region with `visio_test_live_preview`.

Never create a completed VSDX first and open it afterward to simulate live construction.

## Audit

```text
visio_test_live_audit_figure
visio_test_live_get_audit_findings
visio_test_live_get_audit_finding
visio_test_live_set_audit_decision
visio_test_live_apply_safe_patch
```

Audit against the current revision and preview/reference/structure hashes. Pass the Reference Manifest path and the region crop path returned by the reference tools; the live audit verifies both files and computes the regional visual score itself. Bridge-observable text, bounds, metadata, connector type/glue, and raster declarations are read from the current Visio page. Public `objects` fields contain only reference expectations and adverse Reviewer observations: they may add findings, but omission or a benign value is never positive pass evidence. The audit tool returns counts and hard-finding summaries; fetch one detailed record by `finding_id`. Use `set_audit_decision` only to waive an eligible warning/info finding or reopen it. It never signs a region pass.

## Correction

Enter `correcting` only from a failed current region. Apply an offered patch through `visio_test_live_apply_safe_patch` or use exact semantic IDs with targeted tools such as `visio_test_live_update_shape`, `visio_test_live_update_path`, and `visio_test_live_set_cells`. The safe-patch executor currently exposes only the two operations it fully implements: `expand_text_box` and `move_inside_canvas`. Every patch requires the current revision and makes prior evidence stale. After mutation, export a new preview and return to audit.

## Delivery

1. Save the completed visible page with `visio_test_live_save`.
2. Run `visio_test_validate`.
3. Run `visio_test_inspect_file`.
4. Run `visio_test_validate_fidelity`.
5. Export requested files with `visio_test_export`.
6. For high-value work, reopen and verify metadata, grouping, text, paths, and connector glue.
7. Call `visio_test_live_finalize_run_metrics` when a run comparison is requested. Supply the host-observed region/full-page model review counts and real token counts when available; metrics contain bounded tool-response estimates and hashes, never prompts, images, or full paths.

Do not close or shut down a user-owned Visio process. Close the Test Edition session only with an explicit save, discard, or cancel disposition.

## Compact response policy

Return revision, state, hashes, counts, warning codes, handles, and changed semantic IDs by default. Return previews on failures and requested checkpoints. `visio_test_live_get_run_metrics` returns a compact current-run snapshot. Return full capability, object, or finding detail only on demand.
