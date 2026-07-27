# Editable Component Contracts

Use composite tools when repeated scientific structure would otherwise require many model-authored primitive calls. Every component remains a group of independently editable Visio shapes.

## Common input

```json
{
  "semantic_id": "accuracy-chart",
  "region_id": "results",
  "bounds": {"x": 420, "y": 60, "width": 320, "height": 220},
  "style": {},
  "expected_revision": 12
}
```

The parent group stores component type, schema version, data hash, layout version, and ordered child semantic IDs. Child IDs use stable hierarchical names such as:

```text
accuracy-chart.axis.x
accuracy-chart.series.method-a.point.1
accuracy-chart.legend.method-a
```

## Tables

Use `visio_test_live_add_table`, `visio_test_live_update_table_cell`, and `visio_test_live_update_table_layout`.

- Support 1–50 rows and 1–50 columns.
- Keep each cell an independent editable shape and text object.
- Preserve child IDs during cell and layout updates.
- Reject unsupported merged cells with `CAPABILITY_NOT_AVAILABLE`.

## Charts

Use `visio_test_live_add_chart` and `visio_test_live_update_chart`.

Supported types are `bar`, `line`, `scatter`, and `error_bar`. Specify series, domains, axis labels, tick policy, legend policy, colors, and error values. Reject missing domains, non-finite values, and inconsistent lengths.

Axes, ticks, labels, titles, bars, points, segments, error marks, and legend entries remain separate Visio objects.

## Matrices and grids

Use:

```text
visio_test_live_add_matrix
visio_test_live_update_matrix_cell
visio_test_live_add_grid
visio_test_live_update_grid
```

Matrices are limited to 50 × 50 and grids to 2500 cells. Each cell and label remains independently editable. Heat colors come from an explicit numeric scale. Updating one cell must not rebuild the component.

## Atomic execution

Planning must be deterministic and COM-free. Execution validates the entire contract, creates all children in one Visio undo scope, writes metadata, groups the children, and increments the page revision exactly once. A failure rolls back the complete component and returns `COMPONENT_CONTRACT_INVALID` or the precise capability error.

Do not substitute an image or embedded external object for any component described here.
