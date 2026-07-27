import test from "node:test";
import assert from "node:assert/strict";
import {
  liveTools,
} from "../../plugins/visio-scientific-illustrator-test/scripts/tool-schemas.mjs";
import {
  validateSchema,
} from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";

const categories = [
  "masters",
  "primitives",
  "connectors",
  "components",
  "images",
  "layers",
  "containers",
  "shapesheet",
  "exports",
  "limitations",
];

test("capability tools expose the stable Test Edition names", () => {
  const names = liveTools.map((tool) => tool.name);

  assert.equal(names.includes("visio_test_live_get_capabilities"), true);
  assert.equal(names.includes("visio_test_live_get_capability_details"), true);
});

test("capability summary accepts no query parameters", () => {
  const tool = liveTools.find(
    (candidate) => candidate.name === "visio_test_live_get_capabilities",
  );

  assert.ok(tool);
  assert.doesNotThrow(() => validateSchema({}, tool.inputSchema));
  assert.throws(() => validateSchema({ category: "masters" }, tool.inputSchema));
});

test("capability details use bounded cursor pagination and fixed categories", () => {
  const tool = liveTools.find(
    (candidate) => candidate.name === "visio_test_live_get_capability_details",
  );

  assert.ok(tool);
  assert.deepEqual(tool.inputSchema.properties.category.enum, categories);
  assert.doesNotThrow(() => validateSchema(
    { category: "masters", cursor: "50", limit: 50 },
    tool.inputSchema,
  ));
  assert.doesNotThrow(() => validateSchema(
    { category: "limitations" },
    tool.inputSchema,
  ));
  assert.throws(() => validateSchema(
    { category: "unknown" },
    tool.inputSchema,
  ));
  assert.throws(() => validateSchema(
    { category: "masters", cursor: "-1" },
    tool.inputSchema,
  ));
  assert.throws(() => validateSchema(
    { category: "masters", limit: 201 },
    tool.inputSchema,
  ));
});
