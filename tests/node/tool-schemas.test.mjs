import test from "node:test";
import assert from "node:assert/strict";
import {
  fileTools,
  liveTools,
} from "../../plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs";
import {
  mapSequenceError,
  mapToolError,
  validateSchema,
} from "../../plugins/visio-scientific-illustrator/scripts/mcp-common.mjs";
import {
  sceneGraphSchema,
  validateSceneGraph,
} from "../../plugins/visio-scientific-illustrator/scripts/scene-schema.mjs";

test("live tool names are stable and Visio-specific", () => {
  assert.deepEqual(liveTools.map((tool) => tool.name), [
    "visio_live_launch",
    "visio_live_status",
    "visio_live_clear",
    "visio_live_add_shape",
    "visio_live_add_image",
    "visio_live_add_path",
    "visio_live_add_connector",
    "visio_live_update_shape",
    "visio_live_update_path",
    "visio_live_delete_shape",
    "visio_live_draw_sequence",
    "visio_live_inspect",
    "visio_live_fit",
    "visio_live_preview",
    "visio_live_save",
    "visio_live_close_document",
    "visio_live_shutdown",
    "visio_live_group_shapes",
    "visio_live_ungroup_shape",
    "visio_live_add_to_container",
    "visio_live_set_z_order",
    "visio_live_assign_layer",
    "visio_live_align_shapes",
    "visio_live_distribute_shapes",
    "visio_live_match_size",
    "visio_live_set_protection",
    "visio_live_duplicate_shape",
    "visio_live_add_scientific_component",
    "visio_live_get_cells",
    "visio_live_set_cells",
    "visio_live_add_connection_point",
    "visio_live_get_geometry",
    "visio_live_draw_scene",
  ]);
  assert.equal(JSON.stringify(liveTools).includes("drawio"), false);
  assert.deepEqual(fileTools.map((tool) => tool.name), [
    "visio_validate",
    "visio_inspect_file",
    "visio_export",
    "visio_compile_drawio_scene",
    "visio_compare_preview",
    "visio_validate_fidelity",
  ]);
});

test("draw scene schema exposes paced regional execution", () => {
  const drawScene = liveTools.find((tool) => tool.name === "visio_live_draw_scene");
  const scene = {
    scene_id: "scene",
    canvas: { width_px: 800, height_px: 500, dpi: 96 },
    style_tokens: {}, layers: ["content"],
    nodes: [{
      kind: "shape", semantic_id: "a", shape: "rectangle",
      bounds: { x: 20, y: 20, width: 100, height: 60 }, layer: "content",
      label: "A", style: { fill: { kind: "solid", color: "#FFFFFF" } },
    }],
    connectors: [],
    regions: [{ id: "main", bounds: { x: 0, y: 0, width: 800, height: 500 }, checkpoint: true }],
  };
  assert.doesNotThrow(() => validateSchema({
    scene, step_delay_ms: 500, start_region: "main", end_region: "main",
    stop_after_checkpoint: true, existing_policy: "error",
  }, drawScene.inputSchema));
  assert.throws(() => validateSchema({ scene, step_delay_ms: 0 }, drawScene.inputSchema));
});

test("controlled ShapeSheet schemas reject formulas and event cells", () => {
  const byName = (name) => liveTools.find((tool) => tool.name === name).inputSchema;
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "panel",
    writes: [
      { cell: "Rounding", value: { kind: "length", number: 8, unit: "pt" } },
      { cell: "LockAspect", value: { kind: "boolean", boolean: true } },
      { cell: "FillForegnd", value: { kind: "color", color: "#245B91" } },
    ],
  }, byName("visio_live_set_cells")));
  assert.throws(() => validateSchema({
    semantic_id: "panel",
    writes: [{ cell: "EventDblClick", value: { kind: "boolean", boolean: true } }],
  }, byName("visio_live_set_cells")));
  assert.throws(() => validateSchema({
    semantic_id: "panel",
    writes: [{ cell: "Rounding", formula: "CALLTHIS()" }],
  }, byName("visio_live_set_cells")));
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "panel", cells: ["FillForegnd", "LineWeight", "LockAspect"],
  }, byName("visio_live_get_cells")));
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "panel", name: "right_upper", x: 1, y: 0.25, direction_x: 1, direction_y: 0,
  }, byName("visio_live_add_connection_point")));
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "wave", include_rows: true,
  }, byName("visio_live_get_geometry")));
});

test("scientific component schema is bounded and editable", () => {
  const component = liveTools.find((tool) => tool.name === "visio_live_add_scientific_component");
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "tensor",
    component: "tensor_cube",
    bounds: { x: 100, y: 100, width: 500, height: 350 },
    options: { rows: 6, columns: 8, primary_color: "#245B91" },
  }, component.inputSchema));
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "wireless",
    component: "wireless_link",
    bounds: { x: 100, y: 100, width: 80, height: 80 },
    options: { count: 3, direction: "left", primary_color: "#245B91" },
  }, component.inputSchema));
  assert.throws(() => validateSchema({
    semantic_id: "tensor",
    component: "tensor_cube",
    bounds: { x: 100, y: 100, width: 500, height: 350 },
    options: { rows: 101 },
  }, component.inputSchema));
  assert.throws(() => validateSchema({
    semantic_id: "x",
    component: "icon",
    bounds: { x: 0, y: 0, width: 10, height: 10 },
  }, component.inputSchema));
});

test("layout schemas expose native Visio composition operations", () => {
  const byName = (name) => liveTools.find((tool) => tool.name === name).inputSchema;

  assert.doesNotThrow(() => validateSchema(
    { semantic_id: "panel", action: "absolute", position: 3 },
    byName("visio_live_set_z_order"),
  ));
  assert.throws(() => validateSchema(
    { semantic_id: "panel", action: "absolute" },
    byName("visio_live_set_z_order"),
  ));
  assert.doesNotThrow(() => validateSchema(
    { semantic_ids: ["a", "b"], layer: "annotations", replace_existing: true },
    byName("visio_live_assign_layer"),
  ));
  assert.doesNotThrow(() => validateSchema(
    { semantic_ids: ["a", "b"], alignment: "middle", reference_semantic_id: "a" },
    byName("visio_live_align_shapes"),
  ));
  assert.doesNotThrow(() => validateSchema(
    { semantic_ids: ["a", "b", "c"], axis: "horizontal", distribution: "equal_gap" },
    byName("visio_live_distribute_shapes"),
  ));
  assert.doesNotThrow(() => validateSchema(
    { semantic_ids: ["a", "b"], reference_semantic_id: "a", dimension: "both" },
    byName("visio_live_match_size"),
  ));
  assert.doesNotThrow(() => validateSchema(
    { semantic_ids: ["a"], fields: { move_x: true, vertex_edit: false } },
    byName("visio_live_set_protection"),
  ));
  assert.throws(() => validateSchema(
    { semantic_ids: ["a"], fields: {} },
    byName("visio_live_set_protection"),
  ));
  assert.doesNotThrow(() => validateSchema(
    { semantic_id: "a", new_semantic_id: "a-copy", offset_x: 20, offset_y: 30 },
    byName("visio_live_duplicate_shape"),
  ));
});

test("schemas reject unknown nested fields and invalid enums", () => {
  const addShape = liveTools.find((tool) => tool.name === "visio_live_add_shape");
  assert.throws(
    () => validateSchema({
      semantic_id: "encoder",
      shape: "rectangle",
      bounds: { x: 1, y: 2, width: 3, height: 4, extra: true },
    }, addShape.inputSchema),
    /\$\.bounds\.extra/,
  );

  const fit = liveTools.find((tool) => tool.name === "visio_live_fit");
  assert.throws(() => validateSchema({ mode: "selection" }, fit.inputSchema), /\$\.mode/);
});

test("shape schema accepts rich Visio styles and rejects mixed legacy fields", () => {
  const addShape = liveTools.find((tool) => tool.name === "visio_live_add_shape");
  const rich = {
    semantic_id: "panel",
    shape: "rounded_rectangle",
    bounds: { x: 10, y: 10, width: 300, height: 200 },
    style: {
      fill: {
        kind: "gradient",
        color: "#0B4A8B",
        secondary_color: "#FFFFFF",
        transparency_percent: 5,
        gradient_angle_deg: 90,
      },
      line: {
        color: "#0B4A8B",
        width_pt: 1.5,
        cap: "round",
        join: "round",
      },
      text_block: { horizontal_align: "left", vertical_align: "middle", left_margin_pt: 8 },
      shadow: { enabled: true, color: "#808080", transparency_percent: 50, offset_x_pt: 2, offset_y_pt: 2 },
      corner: { radius_pt: 8 },
      opacity_percent: 95,
    },
  };

  assert.doesNotThrow(() => validateSchema(rich, addShape.inputSchema));
  assert.throws(() => validateSchema({
    ...rich,
    style: { ...rich.style, fill_color: "#FFFFFF" },
  }, addShape.inputSchema));
});

test("shape schema accepts editable rich text runs", () => {
  const addShape = liveTools.find((tool) => tool.name === "visio_live_add_shape");
  const input = {
    semantic_id: "equation",
    shape: "text",
    bounds: { x: 10, y: 10, width: 300, height: 60 },
    rich_text: {
      text: "A_l^i = log(|H_l| + epsilon)",
      paragraph: { horizontal_align: "center", vertical_align: "middle" },
      runs: [
        { start: 2, length: 1, subscript: true },
        { start: 4, length: 1, superscript: true },
      ],
      fit: "shrink_text",
    },
  };

  assert.doesNotThrow(() => validateSchema(input, addShape.inputSchema));
  assert.throws(() => validateSchema({ ...input, label: "duplicate" }, addShape.inputSchema));
});

test("path schemas expose bounded editable Geometry sections and strict commands", () => {
  const addPath = liveTools.find((tool) => tool.name === "visio_live_add_path");
  const updatePath = liveTools.find((tool) => tool.name === "visio_live_update_path");
  const input = {
    semantic_id: "wave-and-arc",
    bounds: { x: 20, y: 30, width: 500, height: 240 },
    sections: [
      {
        commands: [
          { op: "M", x: 0, y: 120 },
          { op: "C", x: 250, y: 120, x1: 60, y1: 20, x2: 180, y2: 220 },
          { op: "A", x: 500, y: 120, rx: 125, ry: 80, rotation_deg: 15, large_arc: false, sweep: true },
        ],
      },
    ],
    style: { line: { color: "#245B91", width_pt: 1.5 }, fill: { kind: "none" } },
  };

  assert.doesNotThrow(() => validateSchema(input, addPath.inputSchema));
  assert.doesNotThrow(() => validateSchema({
    semantic_id: input.semantic_id,
    sections: input.sections,
  }, updatePath.inputSchema));
  assert.throws(() => validateSchema({
    ...input,
    sections: Array.from({ length: 33 }, () => input.sections[0]),
  }, addPath.inputSchema));
  assert.throws(() => validateSchema({
    ...input,
    sections: [{ commands: Array.from({ length: 2001 }, () => ({ op: "M", x: 0, y: 0 })) }],
  }, addPath.inputSchema));
  assert.throws(() => validateSchema({
    ...input,
    sections: [{ commands: [{ op: "A", x: 10, y: 10, rx: 5, ry: 5, large_arc: false }] }],
  }, addPath.inputSchema));
  assert.throws(() => validateSchema({ semantic_id: input.semantic_id }, updatePath.inputSchema));

  const sequence = liveTools.find((tool) => tool.name === "visio_live_draw_sequence");
  assert.doesNotThrow(() => validateSchema({
    operations: [
      { type: "path", ...input },
      { type: "path_update", semantic_id: input.semantic_id, sections: input.sections },
    ],
    step_delay_ms: 500,
  }, sequence.inputSchema));
});

test("launch open mode requires a file path", () => {
  const launch = liveTools.find((tool) => tool.name === "visio_live_launch");
  assert.throws(() => validateSchema({ mode: "open" }, launch.inputSchema));
  assert.doesNotThrow(() => validateSchema({ mode: "new" }, launch.inputSchema));
  assert.doesNotThrow(() => validateSchema({
    mode: "open",
    file_path: "E:\\figure.vsdx",
    page_name: "Results",
    canvas: { dpi: 120 },
  }, launch.inputSchema));
  assert.throws(() => validateSchema({
    mode: "open",
    file_path: "E:\\figure.vsdx",
    canvas: { width_px: 100 },
  }, launch.inputSchema));
});

test("connector schema exposes Visio routing, connection points, and waypoints", () => {
  const connector = liveTools.find((tool) => tool.name === "visio_live_add_connector");
  assert.doesNotThrow(() => validateSchema({
    semantic_id: "flow",
    source: "input",
    target: "output",
    label: "signal",
    routing: "polyline",
    source_connection_point: 1,
    target_connection_point: 2,
    waypoints: [{ x: 400, y: 200 }, { x: 600, y: 300 }],
  }, connector.inputSchema));
  assert.throws(() => validateSchema({
    semantic_id: "flow",
    source: "input",
    target: "output",
    waypoints: [{ x: 400, y: 200 }],
  }, connector.inputSchema));
  assert.throws(() => validateSchema({
    semantic_id: "flow",
    source: "input",
    target: "output",
    routing: "polyline",
  }, connector.inputSchema));

  assert.doesNotThrow(() => validateSchema({
    semantic_id: "rich-flow",
    source: "input",
    target: "output",
    routing: "right_angle",
    source_anchor: { mode: "normalized", x: 1, y: 0.25 },
    target_anchor: { mode: "auto", name: "left" },
    jump_style: "arc",
    label_position: 0.7,
    avoid_shape_ids: ["obstacle-a", "obstacle-b"],
  }, connector.inputSchema));
  assert.throws(() => validateSchema({
    semantic_id: "mixed",
    source: "input",
    target: "output",
    source_connection_point: 1,
    source_anchor: { mode: "auto" },
  }, connector.inputSchema));
  assert.throws(() => validateSchema({
    semantic_id: "bad-normalized",
    source: "input",
    target: "output",
    source_anchor: { mode: "normalized", x: 1.2, y: 0.5 },
  }, connector.inputSchema));
});

test("file export scope matches the selected Visio export API", () => {
  const exportTool = fileTools.find((tool) => tool.name === "visio_export");
  const common = {
    input_path: "E:\\figure.vsdx",
    output_path: "E:\\figure.png",
  };

  assert.doesNotThrow(() => validateSchema({
    ...common,
    format: "png",
    page_scope: "current",
    width: 2000,
  }, exportTool.inputSchema));
  assert.throws(() => validateSchema({
    ...common,
    format: "png",
    page_scope: "all",
    width: 2000,
  }, exportTool.inputSchema));
  assert.doesNotThrow(() => validateSchema({
    ...common,
    format: "pdf",
    output_path: "E:\\figure.pdf",
    page_scope: "all",
  }, exportTool.inputSchema));
});

test("known Bridge errors preserve their stable fields", () => {
  const mapped = mapToolError(Object.assign(new Error("busy"), {
    bridgeError: {
      code: "VISIO_BUSY",
      message: "Visio 正忙",
      retryable: true,
      details: { hresult: -2147417846 },
    },
  }), "visio_live_status");

  assert.deepEqual(mapped, {
    error: "VISIO_BUSY",
    message: "Visio 正忙",
    retryable: true,
    details: { hresult: -2147417846 },
    tool: "visio_live_status",
  });
});

test("scientific scene schema accepts a complete scene and rejects unknown node kinds", () => {
  const scene = {
    scene_id: "scene",
    canvas: { width_px: 1600, height_px: 900, dpi: 96 },
    style_tokens: { blue: "#0B4A8B" },
    layers: ["content", "connectors"],
    nodes: [
      {
        kind: "shape",
        semantic_id: "input",
        bounds: { x: 10, y: 20, width: 100, height: 60 },
        layer: "content",
        shape: "rectangle",
      },
      {
        kind: "path",
        semantic_id: "wave",
        bounds: { x: 200, y: 100, width: 120, height: 60 },
        layer: "content",
        commands: [{ op: "M", x: 0, y: 30 }, { op: "L", x: 120, y: 30 }],
      },
    ],
    connectors: [{ semantic_id: "flow", source: "input", target: "wave", routing: "straight" }],
    regions: [{ id: "main", bounds: { x: 0, y: 0, width: 1600, height: 900 }, checkpoint: true }],
  };

  assert.doesNotThrow(() => validateSchema(scene, sceneGraphSchema));
  assert.doesNotThrow(() => validateSceneGraph(scene));
  assert.throws(
    () => validateSchema({ ...scene, nodes: [{ ...scene.nodes[0], kind: "unknown" }] }, sceneGraphSchema),
    /kind/,
  );
});

test("scientific scene semantic validation rejects duplicate IDs and undeclared layers", () => {
  const base = {
    scene_id: "scene",
    canvas: { width_px: 100, height_px: 100, dpi: 96 },
    style_tokens: {},
    layers: ["content"],
    nodes: [
      { kind: "shape", semantic_id: "a", bounds: { x: 0, y: 0, width: 20, height: 20 }, layer: "content", shape: "rectangle" },
    ],
    connectors: [],
    regions: [{ id: "main", bounds: { x: 0, y: 0, width: 100, height: 100 }, checkpoint: true }],
  };

  assert.throws(() => validateSceneGraph({
    ...base,
    nodes: [...base.nodes, { ...base.nodes[0] }],
  }), /\$\.nodes\[1\]\.semantic_id/);
  assert.throws(() => validateSceneGraph({
    ...base,
    nodes: [{ ...base.nodes[0], layer: "missing" }],
  }), /\$\.nodes\[0\]\.layer/);
});

test("unknown Bridge error becomes INTERNAL_ERROR without losing its code", () => {
  const mapped = mapToolError(Object.assign(new Error("future error"), {
    bridgeError: { code: "FUTURE_CODE", details: { source: "bridge" } },
  }), "visio_validate");

  assert.equal(mapped.error, "INTERNAL_ERROR");
  assert.deepEqual(mapped.details, { source: "bridge", bridge_code: "FUTURE_CODE" });
});

test("Bridge transport errors preserve stable codes", () => {
  const timeout = Object.assign(new Error("Bridge request timed out."), {
    code: "BRIDGE_TIMEOUT",
    retryable: false,
    details: { command: "save" },
  });

  assert.deepEqual(mapToolError(timeout, "visio_live_save"), {
    error: "BRIDGE_TIMEOUT",
    message: "Bridge request timed out.",
    retryable: false,
    details: { command: "save" },
    tool: "visio_live_save",
  });
  assert.deepEqual(mapSequenceError(timeout), {
    code: "BRIDGE_TIMEOUT",
    message: "Bridge request timed out.",
    retryable: false,
    details: { command: "save" },
  });
});
