---
name: visio-test-orchestrate-scientific-figure
description: Coordinate the complete Visio Scientific Illustrator Test workflow for recreating, designing, revising, validating, saving, and exporting an editable scientific figure in a visible Microsoft Visio window. Use when a user supplies a reference image, paper figure, PDF region, existing VSDX, or figure brief and wants the full low-token Designer, Drawer, Reviewer, and Corrector quality loop.
---

# Orchestrate a Test Edition Scientific Figure

Recommended entry point for end-to-end Test Edition work. Keep role handoffs compact and let tools enforce deterministic rules.

## Boundaries

- Control only the Test Edition-owned visible Microsoft Visio process.
- Use only `visio_test_` tools. Never operate Visio through system mouse, keyboard, window coordinates, or clipboard automation.
- Keep text, vectors, components, groups, and semantic connectors editable. Require genuine glue at both connector endpoints.
- Never accept uncertainty, stale evidence, or a successful draw call as proof of quality.
- Every logical region must pass the Bridge state machine before the next region can start.
- Keep the final whole-page model review mandatory for every quality profile.

## Compact handoff contract

Pass only:

```text
state_handle
plan_handle
reference_manifest_handle
region_id
page_revision
preview_hash
audit_handle
finding_id[]
compact_summary
```

Fetch details by handle only when the active role needs them. Pass no full object inventory, capability catalog, or finding evidence between roles.

## Workflow

1. Call `visio_test_live_get_capabilities`; request capability details only for planned features.
2. Call `visio_test_prepare_reference`, confirm its manifest, and establish logical reference regions.
3. Select `strict`, `adaptive`, or `economy`; set explicit review limits and preview-return policy.
4. Invoke the Designer once for the global plan and region order.
5. For each region allowed by `state_handle`:
   - invoke the Drawer for that region only;
   - run `visio_test_live_audit_figure`;
   - invoke the model Reviewer only when the profile requires it;
   - apply a deterministic safe patch or invoke the Corrector for unresolved findings;
   - re-audit with the new revision and evidence hashes;
   - verify the current region gate with `visio_test_live_assert_quality_gate`.
6. Run a fresh whole-page deterministic audit and the mandatory whole-page model review.
7. Save the visible document, validate structure and fidelity, then export the requested formats.
8. When cost/quality measurement is requested, finalize the bounded local telemetry with `visio_test_live_finalize_run_metrics`.

On `QUALITY_GATE_BLOCKED`, keep the current region active. On stale evidence or revision conflict, discard the decision and collect a fresh preview and audit.

## Role ownership

- Designer: scientific decomposition and representation choices; no drawing.
- Drawer: visible native Visio construction; no self-approval.
- Reviewer: deterministic and model judgment; no page mutation.
- Corrector: minimal finding-driven changes; no self-approval.
- Orchestrator: budgets, transitions, final delivery, and concise user reporting.

## References

Read only the references required for the current decision:

- Read [quality-contract.md](../../references/quality-contract.md) for state transitions, revision checks, and final acceptance.
- Read [quality-profiles.md](../../references/quality-profiles.md) when selecting or escalating review.
- Read [tool-workflows.md](../../references/tool-workflows.md) for legal tool sequences and compact responses.
- Read [raster-assets.md](../../references/raster-assets.md) only when an image asset is proposed or present.
