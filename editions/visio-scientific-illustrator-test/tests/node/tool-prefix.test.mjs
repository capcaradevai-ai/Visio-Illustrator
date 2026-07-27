import assert from "node:assert/strict";
import test from "node:test";
import {
  fileTools,
  liveTools,
} from "../../plugins/visio-scientific-illustrator-test/scripts/tool-schemas.mjs";
import {
  mapSequenceError,
  mapToolError,
} from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";

test("all Test live tools use the isolated public prefix", () => {
  assert.ok(liveTools.length >= 30);
  assert.ok(
    liveTools.every((tool) => tool.name.startsWith("visio_test_live_")),
  );
  const stablePrefix = `${"visio"}_${"live"}_`;
  assert.equal(liveTools.some((tool) => tool.name.startsWith(stablePrefix)), false);
});

test("all Test file tools use the isolated public prefix", () => {
  assert.ok(fileTools.length >= 5);
  assert.ok(
    fileTools.every(
      (tool) =>
        tool.name.startsWith("visio_test_") &&
        !tool.name.startsWith("visio_test_live_"),
    ),
  );
});

test("mapped errors preserve Test tool identities", () => {
  const toolError = mapToolError(
    Object.assign(new Error("failure"), { code: "INVALID_ARGUMENT" }),
    "visio_test_live_add_shape",
  );
  assert.equal(toolError.tool, "visio_test_live_add_shape");

  const sequenceError = mapSequenceError(
    Object.assign(new Error("failure"), { code: "INVALID_ARGUMENT" }),
  );
  assert.equal(sequenceError.tool, "visio_test_live_draw_sequence");
});
