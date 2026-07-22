import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";

const fixtureDir = path.resolve("tests/fixtures/scientific-scene");

test("scientific fidelity fixture contains the required editable scene roles", async () => {
  const scene = JSON.parse(await fs.readFile(path.join(fixtureDir, "reference-scene.json"), "utf8"));

  assert.deepEqual(scene.canvas, { width_px: 1600, height_px: 900, dpi: 96 });
  assert.equal(new Set(scene.nodes.map((node) => node.semantic_id)).size, scene.nodes.length);
  for (const role of [
    "number_badge",
    "tensor_cube",
    "joint_skeleton",
    "video_frame_stack",
    "latent_vector",
    "waveform",
  ]) {
    assert.ok(scene.nodes.some((node) => node.role === role), `missing role: ${role}`);
  }
  assert.ok(scene.nodes.some((node) => node.rich_text?.runs?.some((run) => run.subscript)));
  assert.ok(scene.nodes.some((node) => node.rich_text?.runs?.some((run) => run.superscript)));
  assert.ok(scene.connectors.some((connector) => connector.glued === true));
});

test("scientific fidelity fixture includes a 1600 by 900 PNG preview", async () => {
  const png = await fs.readFile(path.join(fixtureDir, "reference-preview.png"));
  assert.equal(png.toString("ascii", 1, 4), "PNG");
  assert.equal(png.readUInt32BE(16), 1600);
  assert.equal(png.readUInt32BE(20), 900);
});
