import test from "node:test";
import assert from "node:assert/strict";
import {
  fileTools,
  liveTools,
} from "../../plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs";
import {
  mapToolError,
  validateSchema,
} from "../../plugins/visio-scientific-illustrator/scripts/mcp-common.mjs";

test("live tool names are stable and Visio-specific", () => {
  assert.deepEqual(liveTools.map((tool) => tool.name), [
    "visio_live_launch",
    "visio_live_status",
    "visio_live_clear",
    "visio_live_add_shape",
    "visio_live_add_image",
    "visio_live_add_connector",
    "visio_live_update_shape",
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
  ]);
  assert.equal(JSON.stringify(liveTools).includes("drawio"), false);
  assert.deepEqual(fileTools.map((tool) => tool.name), [
    "visio_validate",
    "visio_inspect_file",
    "visio_export",
  ]);
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

test("launch open mode requires a file path", () => {
  const launch = liveTools.find((tool) => tool.name === "visio_live_launch");
  assert.throws(() => validateSchema({ mode: "open" }, launch.inputSchema), /file_path/);
  assert.doesNotThrow(() => validateSchema({ mode: "new" }, launch.inputSchema));
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

test("unknown Bridge error becomes INTERNAL_ERROR without losing its code", () => {
  const mapped = mapToolError(Object.assign(new Error("future error"), {
    bridgeError: { code: "FUTURE_CODE", details: { source: "bridge" } },
  }), "visio_validate");

  assert.equal(mapped.error, "INTERNAL_ERROR");
  assert.deepEqual(mapped.details, { source: "bridge", bridge_code: "FUTURE_CODE" });
});
