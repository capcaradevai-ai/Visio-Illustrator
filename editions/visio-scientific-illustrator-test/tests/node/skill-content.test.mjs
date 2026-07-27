import test from "node:test";
import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const pluginRoot = path.join(
  root,
  "plugins/visio-scientific-illustrator-test",
);

async function readMarkdownCorpus() {
  const skillsRoot = path.join(pluginRoot, "skills");
  const referencesRoot = path.join(pluginRoot, "references");
  const skillDirectories = await readdir(skillsRoot, { withFileTypes: true });
  const referenceFiles = await readdir(referencesRoot, { withFileTypes: true });
  const paths = [
    ...skillDirectories
      .filter((entry) => entry.isDirectory())
      .map((entry) => path.join(skillsRoot, entry.name, "SKILL.md")),
    ...referenceFiles
      .filter((entry) => entry.isFile() && entry.name.endsWith(".md"))
      .map((entry) => path.join(referencesRoot, entry.name)),
  ];
  return (await Promise.all(paths.map((file) => readFile(file, "utf8")))).join(
    "\n",
  );
}

test("five-role Visio skill corpus preserves mature backend and quality contracts", async () => {
  const corpus = await readMarkdownCorpus();
  for (const required of [
    "visible Microsoft Visio",
    "SceneGraph",
    "primitive budget",
    "rich text",
    "editable paths",
    "final whole-page model review",
    "User Cells",
    "Shape Data",
    "genuine glue",
    "state_handle",
    "page_revision",
    "preview_hash",
    "finding_id",
    "raster_reason",
    "atomic_raster_unit",
    "contains_reconstructable_content",
    "visio_test_live_get_capabilities",
    "visio_test_prepare_reference",
    "visio_test_live_draw_scene",
    "visio_test_live_add_image",
    "visio_test_live_audit_figure",
    "visio_test_live_set_audit_decision",
    "visio_test_validate_fidelity",
    "visio_test_live_save",
    "visio_test_export",
    "visio_test_live_add_table",
    "visio_test_live_add_chart",
    "visio_test_live_add_matrix",
    "visio_test_live_add_grid",
  ]) {
    assert.match(corpus, new RegExp(required.replaceAll(".", "\\.")));
  }
  for (const forbidden of [
    "CDP",
    "mxCell",
    "mxGraph",
    "mxCodec",
    "graph_ready",
    "drawio_live_",
    "powerpoint_",
  ]) {
    assert.equal(corpus.includes(forbidden), false, forbidden);
  }
});
