import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const skillPath = path.join(
  root,
  "plugins/visio-scientific-illustrator/skills/recreate-scientific-figure-in-visio/SKILL.md",
);

test("Visio skill contains required boundaries and no active legacy terminology", async () => {
  const skill = await readFile(skillPath, "utf8");
  for (const required of [
    "Visio COM automation",
    "SceneGraph",
    "primitive budget",
    "rich text",
    "editable freeform path",
    "region checkpoint",
    "local image policy",
    "visual comparison",
    "final acceptance gate",
    "User.CodexId",
    "CellsU",
    "FormulaU",
    "glued 1D connectors",
    "visio_live_draw_scene",
    "visio_live_add_scientific_component",
    "visio_live_add_path",
    "visio_compare_preview",
    "visio_validate_fidelity",
    "visio_live_preview",
    "visio_live_save",
  ]) {
    assert.match(skill, new RegExp(required.replaceAll(".", "\\.")));
  }
  for (const forbidden of [
    "CDP",
    "mxCell",
    "mxCodec",
    "graph_ready",
    "drawio_live_",
    "ASCII art",
    "ellipse-as-cylinder",
    "SSIM-only",
  ]) {
    assert.equal(skill.includes(forbidden), false, forbidden);
  }
});
