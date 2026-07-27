---
name: visio-test-draw-scientific-figure-live
description: Execute an approved region plan through Visio Scientific Illustrator Test in a plugin-owned visible Microsoft Visio page using editable native objects and genuinely glued connectors. Use for initial construction or precise object-level updates after the Bridge authorizes the region state.
---

# Draw a Test Edition Scientific Figure Live

Construct only the authorized region in the visible page. Do not approve your own result.

## Preconditions

Require:

```text
state_handle
plan_handle
region_id
expected_revision
step_delay_ms
```

Confirm that the Bridge authorizes `drawing` or `correcting` for this region. Refuse stale revisions and do not bypass `QUALITY_GATE_BLOCKED`.

## Drawing rules

- Use `visio_test_live_launch` only for a new Test Edition-owned session; otherwise inspect its status.
- Use the exact user-requested pacing. Default user-facing visible steps to 500 ms.
- Build backgrounds before structures, structures before connectors, and labels after geometry.
- Keep scientific text as editable Visio text and custom contours as editable paths.
- Use editable composite tools for tables, charts, matrices, and grids.
- Create semantic arrows as 1D connectors with explicit source and target IDs; verify both endpoints are glued.
- Preserve semantic IDs, layers, groups, ShapeSheet metadata, z-order, and accepted content outside the active region.
- Use an image only with a validated atomic raster declaration and a tightly cropped registered source.
- Refresh and export a region preview after coherent construction.

Prefer `visio_test_live_draw_scene` for a stored SceneGraph that contains a planned multi-object region. Use atomic tools only for small additions or precise corrections.

## Output

Return:

```text
state_handle
region_id
page_revision
preview_handle
preview_hash
changed_semantic_ids[]
glue_summary
warning_codes[]
```

Return summaries and handles, not complete page geometry. Transition to `awaiting_audit`; never sign pass.

## References

Read only the references required for the active region:

- Read [tool-workflows.md](../../references/tool-workflows.md) for visible construction, correction, and delivery tool order.
- Read [component-contracts.md](../../references/component-contracts.md) when drawing or updating a composite component.
- Read [quality-contract.md](../../references/quality-contract.md) for revision and region transition rules.
- Read [raster-assets.md](../../references/raster-assets.md) before calling `visio_test_live_add_image`.
