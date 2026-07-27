# Finding Taxonomy

Use stable findings to make review and correction reproducible across model turns.

## Required record

```json
{
  "finding_id": "connector_clearance:region-a:flow-2:sha256",
  "severity": "hard",
  "category": "connector_clearance",
  "region_id": "region-a",
  "object_ids": ["flow-2", "label-3"],
  "observed_revision": 12,
  "evidence": {},
  "required_change": "Route flow-2 outside label-3.",
  "acceptance_condition": "Clearance is at least the configured minimum.",
  "safe_patch": null
}
```

The Bridge derives `finding_id` deterministically from category, region, stable object IDs, and normalized evidence. Page-local numeric shape IDs must not define identity.

## Severity

- `hard`: blocks the region or final page.
- `soft`: requires correction unless an explicit scientific or visual justification is recorded.
- `info`: does not block but remains visible in delivery notes.

Budget exhaustion never reduces severity.

## Categories

### Semantic

`missing_semantic_object`, `unexpected_text`, `text_mismatch`, `scientific_direction_uncertain`, `legend_mismatch`

### Topology

`unglued_connector`, `wrong_connector_direction`, `connector_crossing`, `connector_clearance`, `route_ambiguity`

### Geometry and typography

`bounds_mismatch`, `alignment`, `spacing`, `overlap`, `text_overflow`, `text_fit`, `line_clearance`, `z_order`

### Editability and metadata

`flattened_content`, `component_contract`, `semantic_id`, `raster_declaration`, `audit_state`

### Visual

`reference_difference`, `color_difference`, `edge_difference`, `foreground_coverage`, `visual_uncertainty`

## Lifecycle

```text
open -> correction_applied -> resolved
open -> superseded
open -> accepted_with_justification
```

Only a fresh re-audit can mark a finding `resolved`. A mutation cannot resolve its own finding. Use `visio_test_live_get_audit_finding` for one detailed record and `visio_test_live_get_audit_findings` for compact filtered summaries.

## Safe patches

A safe patch must name exact semantic IDs, typed fields, expected revision, deterministic values, and its acceptance condition. Never auto-apply deletion, scientific relabeling, representation replacement, rasterization, or uncertain connector rerouting.
