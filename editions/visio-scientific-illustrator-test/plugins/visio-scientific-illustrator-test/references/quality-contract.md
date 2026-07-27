# Quality Contract

Use this contract whenever a region or whole page changes state. The Bridge, not a role narrative, owns the authoritative state.

## Region state machine

```text
planned
  -> drawing
  -> awaiting_audit
  -> passed

awaiting_audit
  -> failed
  -> correcting
  -> awaiting_audit
```

Only the current allowed region may enter `drawing`. A later region receives `QUALITY_GATE_BLOCKED` until every dependency is `passed`.

## Revision and evidence

Every successful page mutation increments `page_revision`. A review decision binds to:

```text
page_revision
preview_hash
reference_region_hash
structure_hash
region_id
```

Any mismatch makes the evidence stale. Re-export and call `visio_test_live_audit_figure`; never reuse a prior decision. `REVISION_CONFLICT` and `STALE_AUDIT_EVIDENCE` are blocking results.

## Regional gate

1. Confirm `state_handle` and expected revision.
2. Draw or correct the authorized region.
3. Export a coherent regional preview.
4. Run deterministic audit.
5. Run model review when the selected profile requires it.
6. Resolve every blocking finding.
7. Re-audit after any mutation.
8. Confirm the Bridge-owned pass with `visio_test_live_assert_quality_gate`.

A pass requires exact text, required semantics, valid component structure, editable native objects, correct z-order, acceptable visual evidence, and genuinely glued semantic connectors.

## Whole-page gate

After all regions pass:

1. collect a new whole-page preview;
2. run deterministic whole-page audit;
3. run one whole-page model review;
4. resolve and re-audit any new finding;
5. save the visible VSDX;
6. validate structure, metadata, fidelity, and serialization;
7. export requested outputs.

The final model review is mandatory for `strict`, `adaptive`, and `economy`.

## Handle discipline

Roles exchange `state_handle`, `audit_handle`, `finding_id`, and compact counts. Retrieve full evidence or object lists only for the active decision. Never infer state from a screenshot or prose summary.
