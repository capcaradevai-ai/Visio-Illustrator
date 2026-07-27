---
name: visio-test-design-scientific-figure
description: Design a compact, capability-aware reconstruction plan for Visio Scientific Illustrator Test without mutating the page. Use when a scientific reference or brief must be decomposed into logical regions, semantic objects, editable Visio primitives, composite components, connectors, and narrowly justified raster assets before visible drawing begins.
---

# Design a Test Edition Scientific Figure

Translate the reference and scientific intent into an executable region plan. Do not execute drawing operations.

## Inputs

Require:

```text
reference_manifest_handle
capability_summary
canvas_bounds
quality_profile
delivery_requirements
```

Call `visio_test_live_get_capability_details` only for a feature that affects a representation choice. Inspect reference regions by handle rather than repeatedly loading the full image.

## Responsibilities

1. Establish aspect ratio, reading direction, visual hierarchy, palette, and typography roles.
2. Partition the page into ordered logical regions with explicit dependencies.
3. Give every meaningful object a stable semantic ID and role.
4. Choose native Visio shapes, paths, text, glued connectors, groups, layers, or editable composite components.
5. Define connector sources, targets, direction, routing intent, and required connection points.
6. Estimate a credible primitive budget and identify the most complex region for adaptive review.
7. Mark ambiguous scientific semantics and low-confidence representation choices for Reviewer escalation.
8. Propose a raster asset only when its internal pixels are not required to be editable and the atomic-image contract can be satisfied.

Do not approve regions, change state, or invent unavailable capabilities.

## Output

Return a compact plan:

```text
plan_handle
region_order[]
region_summary[]
shared_style_tokens
capability_requirements[]
complexity_ranking[]
ambiguity_flags[]
raster_asset_ids[]
```

Each region summary contains bounds, dependencies, semantic ID prefixes, component counts, connector intent, and acceptance checks. Leave detailed primitive payloads in the stored plan.

## References

Read only the references required for the active representation decision:

- Read [component-contracts.md](../../references/component-contracts.md) before selecting an editable table, chart, matrix, or grid.
- Read [quality-contract.md](../../references/quality-contract.md) when defining regions and acceptance checks.
- Read [quality-profiles.md](../../references/quality-profiles.md) when ranking complexity or ambiguity.
- Read [raster-assets.md](../../references/raster-assets.md) before proposing any image asset.
