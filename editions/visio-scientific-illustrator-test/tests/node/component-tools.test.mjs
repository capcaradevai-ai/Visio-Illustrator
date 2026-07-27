import test from "node:test";
import assert from "node:assert/strict";

import {
  liveTools,
} from "../../plugins/visio-scientific-illustrator-test/scripts/tool-schemas.mjs";
import {
  validateSchema,
} from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";

const names = [
  "visio_test_live_add_table",
  "visio_test_live_update_table_cell",
  "visio_test_live_update_table_layout",
  "visio_test_live_add_chart",
  "visio_test_live_update_chart",
  "visio_test_live_add_matrix",
  "visio_test_live_update_matrix_cell",
  "visio_test_live_add_grid",
  "visio_test_live_update_grid",
];

test("editable component tools expose bounded high-level contracts", () => {
  for (const name of names)
    assert.ok(liveTools.some((tool) => tool.name === name), name);

  const table = schema("visio_test_live_add_table");
  assert.doesNotThrow(() => validateSchema({
    component: component("table"),
    cells: [[{ text: "A" }, { text: "B" }]],
    layout: { header_row: true },
  }, table));

  const chart = schema("visio_test_live_add_chart");
  assert.doesNotThrow(() => validateSchema({
    component: component("chart"),
    chart_type: "error_bar",
    series: [{
      name: "ours",
      x: [0, 1],
      y: [0.5, 0.8],
      error: [0.05, 0.04],
      color: "#2F75B5",
    }],
    x_axis: { label: "Epoch", tick_count: 5 },
    y_axis: { label: "Accuracy", minimum: 0, maximum: 1, tick_count: 5 },
    legend: { show: true, position: "right" },
  }, chart));

  const matrix = schema("visio_test_live_add_matrix");
  assert.doesNotThrow(() => validateSchema({
    component: component("matrix"),
    values: [[0, 0.5], [0.75, 1]],
    heatmap: true,
    color_scale: {
      minimum: 0,
      maximum: 1,
      minimum_color: "#FFFFFF",
      maximum_color: "#FF0000",
    },
  }, matrix));

  const grid = schema("visio_test_live_add_grid");
  assert.throws(() => validateSchema({
    component: component("grid"),
    rows: 51,
    columns: 50,
  }, grid));
});

function schema(name) {
  return liveTools.find((tool) => tool.name === name).inputSchema;
}

function component(componentType) {
  return {
    semantic_id: `${componentType}-1`,
    component_type: componentType,
    region_id: "main",
    bounds: { x: 10, y: 10, width: 300, height: 200 },
    expected_revision: 0,
  };
}
