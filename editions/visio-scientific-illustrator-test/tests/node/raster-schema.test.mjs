import test from "node:test";
import assert from "node:assert/strict";

import {
  liveTools,
} from "../../plugins/visio-scientific-illustrator-test/scripts/tool-schemas.mjs";
import {
  validateSchema,
} from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";
import {
  sceneGraphSchema,
} from "../../plugins/visio-scientific-illustrator-test/scripts/scene-schema.mjs";

const declaration = {
  asset_id: "router-icon-1",
  source_type: "reference_crop",
  raster_reason: "Complex small illustration is retained as one object.",
  source_is_tightly_cropped: true,
  atomic_raster_unit: true,
  contains_reconstructable_content: false,
  decomposition_note: "Text, border and connectors were rebuilt as Visio objects.",
  editable_scope: "object_only",
  source_reference_sha256: "a".repeat(64),
  source_bounds: { x: 12, y: 20, width: 80, height: 60 },
  asset_sha256: "b".repeat(64),
  semantic_role: "wireless_router",
};
const {
  source_reference_sha256: _sourceReferenceSha256,
  source_bounds: _sourceBounds,
  ...declarationWithoutReference
} = declaration;
const userFileDeclaration = {
  ...declarationWithoutReference,
  source_type: "user_file",
};

test("add_image requires a complete atomic raster declaration", () => {
  const schema = liveTools.find(
    (tool) => tool.name === "visio_test_live_add_image",
  ).inputSchema;
  const input = {
    semantic_id: "router",
    file_path: "E:\\assets\\router.png",
    bounds: { x: 10, y: 10, width: 64, height: 64 },
  };

  assert.throws(() => validateSchema(input, schema));
  assert.doesNotThrow(() => validateSchema({
    ...input,
    declaration,
  }, schema));
  assert.throws(() => validateSchema({
    ...input,
    declaration: { ...declaration, atomic_raster_unit: false },
  }, schema));
  assert.throws(() => validateSchema({
    ...input,
    declaration: { ...declaration, contains_reconstructable_content: true },
  }, schema));
});

test("SceneGraph image nodes enforce the same declaration", () => {
  const scene = {
    scene_id: "raster",
    canvas: { width_px: 200, height_px: 100, dpi: 96 },
    style_tokens: {},
    layers: ["content"],
    nodes: [{
      kind: "image",
      semantic_id: "router",
      bounds: { x: 10, y: 10, width: 64, height: 64 },
      file_path: "E:\\assets\\router.png",
      layer: "content",
    }],
    connectors: [],
    regions: [{
      id: "main",
      bounds: { x: 0, y: 0, width: 200, height: 100 },
      checkpoint: true,
    }],
  };

  assert.throws(() => validateSchema(scene, sceneGraphSchema));
  assert.doesNotThrow(() => validateSchema({
    ...scene,
    nodes: [{ ...scene.nodes[0], declaration }],
  }, sceneGraphSchema));
});

test("video frame images cannot bypass the raster declaration", () => {
  const schema = liveTools.find(
    (tool) => tool.name === "visio_test_live_add_scientific_component",
  ).inputSchema;
  const input = {
    semantic_id: "frames",
    component: "video_frame_stack",
    bounds: { x: 10, y: 10, width: 160, height: 100 },
    options: { image_path: "E:\\assets\\frame.png" },
  };

  assert.throws(() => validateSchema(input, schema));
  assert.doesNotThrow(() => validateSchema({
    ...input,
    options: {
      ...input.options,
      image_declaration: userFileDeclaration,
    },
  }, schema));
});
