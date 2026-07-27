# Quality Profiles

Profiles change model-review frequency, not deterministic checks, editability, connector glue, or final acceptance.

## Strict

```text
model review: every region
model correction: every failed region
final model review: required
```

Use for complex paper figures, ambiguous scientific mechanisms, and final publication output.

## Adaptive

```text
model review: first-region calibration, highest-complexity region, failure, or uncertainty
model correction: uncertain or unsafe findings
final model review: required
```

This is the target default only after A/B evidence confirms the release thresholds.

## Economy

```text
model review: failure or low confidence
model correction: uncertain or unsafe findings
final model review: required
```

Use for simple figures and drafts. Never use economy to pass uncertainty.

## Default budget

```json
{
  "quality_profile": "strict",
  "max_model_review_rounds_per_region": 1,
  "max_full_page_model_reviews": 1,
  "return_preview_policy": "failures_only",
  "capability_detail_policy": "on_demand",
  "auto_apply_safe_patches": true
}
```

When the model-review limit is reached and uncertainty remains, return a blocked decision.

## Adaptive escalation

Require model review for:

- first-region calibration;
- the highest deterministic complexity score;
- any hard finding;
- visual metrics below threshold or with high uncertainty;
- uncertain scientific meaning, wording, direction, or representation;
- complex connector topology;
- multiple raster assets;
- a complex chart, matrix, or dense grid;
- failure after a safe patch.

If the first region is also the highest-complexity region, count it once.

## Deterministic complexity

The Bridge computes complexity from:

```text
object_count
connector_count
connector_crossing_count
text_density
composite_component_count
raster_asset_count
overlap_density
visual_metric_uncertainty
```

Use the tool-provided score and trigger list. Do not ask the model to recompute weights. `visio_test_live_audit_figure` remains mandatory for every region under every profile.

## Release thresholds

Adaptive may become the default only when the simple Strict/Adaptive A/B check shows at least:

- 50% fewer model image reviews;
- 40% fewer tool-output tokens;
- 30% less total time;
- unchanged structural, glue, and editability results;
- at least 90% of Strict visual quality;
- 100% readable and unambiguous scientific text and semantics.

If any target fails, keep Strict as the default.
