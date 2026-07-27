import { McpValidationError, validateSchema } from "./mcp-common.mjs";
import { rasterDeclarationSchema } from "./raster-schema.mjs";

const semanticId = { type: "string", minLength: 1, maxLength: 120 };
const bounds = {
  type: "object",
  required: ["x", "y", "width", "height"],
  properties: {
    x: { type: "number", minimum: 0 },
    y: { type: "number", minimum: 0 },
    width: { type: "number", exclusiveMinimum: 0 },
    height: { type: "number", exclusiveMinimum: 0 },
  },
  additionalProperties: false,
};

const pathCommand = {
  type: "object",
  required: ["op"],
  properties: {
    op: { type: "string", enum: ["M", "L", "C", "A", "Z"] },
    x: { type: "number" },
    y: { type: "number" },
    x1: { type: "number" },
    y1: { type: "number" },
    x2: { type: "number" },
    y2: { type: "number" },
    rx: { type: "number", exclusiveMinimum: 0 },
    ry: { type: "number", exclusiveMinimum: 0 },
    rotation_deg: { type: "number" },
    large_arc: { type: "boolean" },
    sweep: { type: "boolean" },
  },
  additionalProperties: false,
  allOf: [
    { if: { properties: { op: { enum: ["M", "L"] } } }, then: { required: ["x", "y"] } },
    { if: { properties: { op: { const: "C" } } }, then: { required: ["x1", "y1", "x2", "y2", "x", "y"] } },
    { if: { properties: { op: { const: "A" } } }, then: { required: ["rx", "ry", "x", "y"] } },
  ],
};

const sceneNode = {
  type: "object",
  required: ["kind", "semantic_id", "bounds"],
  properties: {
    kind: { type: "string", enum: ["shape", "text", "path", "image", "component", "group"] },
    semantic_id: semanticId,
    bounds,
    layer: { type: "string", minLength: 1, maxLength: 120 },
    z_order: { type: "integer", minimum: 0 },
    role: { type: "string", minLength: 1, maxLength: 120 },
    parent_id: semanticId,
    shape: { type: "string", minLength: 1, maxLength: 120 },
    label: { type: "string", maxLength: 10000 },
    style: { type: "object" },
    rich_text: { type: "object" },
    text: { type: "string", maxLength: 10000 },
    commands: { type: "array", minItems: 1, maxItems: 2000, items: pathCommand },
    closed: { type: "boolean" },
    file_path: { type: "string", minLength: 3 },
    declaration: rasterDeclarationSchema,
    parameters: { type: "object" },
  },
  additionalProperties: false,
  allOf: [
    { if: { properties: { kind: { const: "shape" } } }, then: { required: ["shape"] } },
    { if: { properties: { kind: { const: "text" } } }, then: { required: ["text"] } },
    { if: { properties: { kind: { const: "path" } } }, then: { required: ["commands"] } },
    { if: { properties: { kind: { const: "image" } } }, then: { required: ["file_path", "declaration"] } },
    { if: { properties: { kind: { const: "component" } } }, then: { required: ["role"] } },
  ],
};

export const sceneGraphSchema = {
  type: "object",
  required: ["scene_id", "canvas", "style_tokens", "layers", "nodes", "connectors", "regions"],
  properties: {
    scene_id: { type: "string", minLength: 1, maxLength: 120 },
    canvas: {
      type: "object",
      required: ["width_px", "height_px", "dpi"],
      properties: {
        width_px: { type: "integer", minimum: 1, maximum: 100000 },
        height_px: { type: "integer", minimum: 1, maximum: 100000 },
        dpi: { type: "number", exclusiveMinimum: 0, maximum: 2400 },
      },
      additionalProperties: false,
    },
    style_tokens: { type: "object" },
    layers: { type: "array", minItems: 1, maxItems: 128, items: { type: "string", minLength: 1, maxLength: 120 } },
    nodes: { type: "array", minItems: 1, maxItems: 10000, items: sceneNode },
    connectors: {
      type: "array",
      maxItems: 10000,
      items: {
        type: "object",
        required: ["semantic_id", "source", "target", "routing"],
        properties: {
          semantic_id: semanticId,
          source: semanticId,
          target: semanticId,
          routing: { type: "string", enum: ["dynamic", "right_angle", "straight", "polyline"] },
          glued: { type: "boolean" },
          label: { type: "string", maxLength: 10000 },
          style: { type: "object" },
          waypoints: {
            type: "array", minItems: 1, maxItems: 1000,
            items: {
              type: "object", required: ["x", "y"],
              properties: { x: { type: "number" }, y: { type: "number" } },
              additionalProperties: false,
            },
          },
          source_anchor: { type: "object" },
          target_anchor: { type: "object" },
        },
        additionalProperties: false,
      },
    },
    regions: {
      type: "array",
      minItems: 1,
      maxItems: 256,
      items: {
        type: "object",
        required: ["id", "bounds", "checkpoint"],
        properties: {
          id: { type: "string", minLength: 1, maxLength: 120 },
          bounds,
          checkpoint: { type: "boolean" },
        },
        additionalProperties: false,
      },
    },
  },
  additionalProperties: false,
};

export function validateSceneGraph(scene) {
  validateSchema(scene, sceneGraphSchema);
  const identifiers = new Set();
  const layers = new Set();
  scene.layers.forEach((layer, index) => {
    if (layers.has(layer)) fail(`$.layers[${index}]`, "图层名称必须唯一");
    layers.add(layer);
  });

  const nodes = new Map();
  scene.nodes.forEach((node, index) => {
    addIdentifier(identifiers, node.semantic_id, `$.nodes[${index}].semantic_id`);
    nodes.set(node.semantic_id, node);
    validateBounds(node.bounds, scene.canvas, `$.nodes[${index}].bounds`);
    if (node.layer !== undefined && !layers.has(node.layer)) {
      fail(`$.nodes[${index}].layer`, `未声明图层: ${node.layer}`);
    }
    if (node.kind === "path") validatePath(node, index);
  });

  scene.nodes.forEach((node, index) => validateParent(node, index, nodes));
  scene.connectors.forEach((connector, index) => {
    const path = `$.connectors[${index}]`;
    addIdentifier(identifiers, connector.semantic_id, `${path}.semantic_id`);
    if (!nodes.has(connector.source)) fail(`${path}.source`, "连接线源 Shape 不存在");
    if (!nodes.has(connector.target)) fail(`${path}.target`, "连接线目标 Shape 不存在");
    if (connector.source === connector.target) fail(path, "连接线源和目标必须不同");
  });

  const regionIds = new Set();
  scene.regions.forEach((region, index) => {
    if (regionIds.has(region.id)) fail(`$.regions[${index}].id`, "区域 ID 必须唯一");
    regionIds.add(region.id);
    validateBounds(region.bounds, scene.canvas, `$.regions[${index}].bounds`);
  });
  if (!scene.regions.some((region) => region.checkpoint)) fail("$.regions", "至少需要一个预览检查点区域");
  return scene;
}

function validatePath(node, nodeIndex) {
  const base = `$.nodes[${nodeIndex}].commands`;
  if (node.commands[0].op !== "M") fail(`${base}[0].op`, "路径首命令必须是 M");
  node.commands.forEach((command, commandIndex) => {
    for (const [xName, yName] of [["x", "y"], ["x1", "y1"], ["x2", "y2"]]) {
      if (command[xName] === undefined && command[yName] === undefined) continue;
      if (command[xName] === undefined || command[yName] === undefined
          || command[xName] < 0 || command[yName] < 0
          || command[xName] > node.bounds.width || command[yName] > node.bounds.height) {
        fail(`${base}[${commandIndex}]`, "路径局部坐标必须位于节点 bounds 内");
      }
    }
  });
}

function validateParent(node, index, nodes) {
  if (node.parent_id === undefined) return;
  if (!nodes.has(node.parent_id)) fail(`$.nodes[${index}].parent_id`, "父节点不存在");
  const seen = new Set([node.semantic_id]);
  let current = node;
  while (current.parent_id !== undefined) {
    if (seen.has(current.parent_id)) fail(`$.nodes[${index}].parent_id`, "节点父子关系存在循环");
    seen.add(current.parent_id);
    current = nodes.get(current.parent_id);
  }
}

function validateBounds(box, canvas, path) {
  if (box.x + box.width > canvas.width_px || box.y + box.height > canvas.height_px) {
    fail(path, "边界必须位于画布内");
  }
}

function addIdentifier(identifiers, identifier, path) {
  if (identifiers.has(identifier)) fail(path, `重复语义 ID: ${identifier}`, "DUPLICATE_SEMANTIC_ID");
  identifiers.add(identifier);
}

function fail(fieldPath, message, code = "INVALID_ARGUMENT") {
  const error = new McpValidationError(`${fieldPath}: ${message}`, fieldPath);
  error.code = code;
  throw error;
}
