---
name: visio-test-audit-scientific-figure
description: Review a Visio Scientific Illustrator Test region or whole page using revision-bound structural, semantic, topology, editability, and visual evidence. Use after drawing or correction, for profile-triggered model review, or before final save and export when the page must be judged without being changed.
---

# Audit a Test Edition Scientific Figure

Judge the current evidence independently. Do not modify the Visio page.

## Inputs

Require:

```text
state_handle
region_id_or_whole_page
page_revision
preview_hash
reference_region_hash
quality_profile
```

Reject stale revision, preview, reference, or structure hashes. Call `visio_test_live_audit_figure` for every region and for the final whole page.

## Review sequence

1. Verify exact text, semantic IDs, expected object roles, and component structure.
2. Verify connector direction, route clearance, and genuine glue at both endpoints.
3. Verify editability, group intent, metadata, bounds, alignment, text fit, and line clearance.
4. Compare fresh regional evidence with the matching reference region.
5. Emit stable findings with severity, object IDs, evidence, required change, and acceptance condition.
6. Apply the selected profile: deterministic review always; model review only when required.
7. Sign `pass` only when no blocking finding remains and evidence matches the current revision.

Fetch one detailed record with `visio_test_live_get_audit_finding` only when the finding cannot be judged from its compact summary.

## Independence

- Do not repair, move, resize, restyle, reroute, delete, or create objects.
- Do not lower thresholds because the review budget is exhausted.
- Do not accept missing scientific semantics solely because visual similarity is high.
- Do not accept structurally editable output solely because visual similarity is low-cost to compute.

## Output

Return:

```text
audit_handle
observed_revision
evidence_hashes
decision
severity_counts
finding_id[]
model_review_required
compact_rationale
```

Keep evidence details behind finding handles. A failed region returns to the Corrector through the Orchestrator.

## References

Read only the references required for the current audit:

- Read [finding-taxonomy.md](../../references/finding-taxonomy.md) for finding fields, severity, and lifecycle.
- Read [quality-contract.md](../../references/quality-contract.md) for fresh-evidence and pass rules.
- Read [quality-profiles.md](../../references/quality-profiles.md) for model escalation.
- Read [raster-assets.md](../../references/raster-assets.md) when auditing an image declaration.
