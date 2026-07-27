---
name: visio-test-correct-scientific-figure
description: Convert Visio Scientific Illustrator Test findings into minimal ordered object-level corrections and apply only authorized safe changes. Use when a current region has failed audit and its stable finding IDs must be resolved while preserving accepted Visio objects, semantic IDs, editability, and connector glue.
---

# Correct a Test Edition Scientific Figure

Resolve current findings with the smallest safe change set. Do not approve your own correction.

## Inputs

Require:

```text
state_handle
region_id
expected_revision
finding_id[]
preserve_semantic_ids[]
```

Retrieve detailed evidence only for active findings through `visio_test_live_get_audit_finding`. Refuse stale findings and changes outside the failed region.

## Correction procedure

1. Order changes so geometry precedes connector routing and text fit follows final bounds.
2. Prefer a deterministic safe patch through `visio_test_live_apply_safe_patch` when the finding provides one.
3. Otherwise choose precise Drawer operations on named semantic IDs.
4. Preserve accepted objects, stable IDs, component child IDs, grouping, layers, metadata, and intentional z-order.
5. Recheck connector source, target, direction, waypoints, and both glue endpoints after any related move.
6. Export a fresh region preview and transition back to `awaiting_audit`.

Use targeted operations such as `visio_test_live_update_shape`, `visio_test_live_update_path`, or typed ShapeSheet updates. Never rebuild a whole region to correct one object unless the finding explicitly proves the region contract is invalid.

## Safety boundary

- Apply auto-patches only through `visio_test_live_apply_safe_patch`; the tool must reject stale revisions, closed findings, object mismatches, and operations outside its allowlist.
- Escalate scientific meaning, representation ambiguity, uncertain routing, or uncertain deletion to the model.
- Never hide a defect with a large image, background cover, tiny text, or disconnected decorative line.
- Never sign pass or suppress a finding; the Reviewer must re-audit fresh evidence.

## Output

Return:

```text
state_handle
new_page_revision
resolved_finding_id[]
changed_semantic_ids[]
preview_handle
preview_hash
remaining_uncertainty[]
```

## References

Read only the references required for the active findings:

- Read [finding-taxonomy.md](../../references/finding-taxonomy.md) for correction requirements and safe-patch rules.
- Read [tool-workflows.md](../../references/tool-workflows.md) for legal targeted mutation order.
- Read [component-contracts.md](../../references/component-contracts.md) before changing a composite component.
- Read [raster-assets.md](../../references/raster-assets.md) before correcting an image declaration.
