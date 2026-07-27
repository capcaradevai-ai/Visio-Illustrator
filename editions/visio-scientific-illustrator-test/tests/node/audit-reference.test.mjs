import test from "node:test";
import assert from "node:assert/strict";
import {
  copyFile,
  mkdir,
  mkdtemp,
  readFile,
  writeFile,
} from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  computeRegionalVisualScore,
  loadAuditReference,
} from "../../plugins/visio-scientific-illustrator-test/scripts/audit-reference.mjs";
import {
  computeManifestSha256,
  sha256Hex,
} from "../../plugins/visio-scientific-illustrator-test/scripts/reference-schema.mjs";
import { decodePng } from "../../plugins/visio-scientific-illustrator-test/scripts/visual-compare.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

test("audit reference is bound to a valid manifest, region, source, and crop", async () => {
  const sourcePath = path.join(
    root,
    "tests/fixtures/scientific-scene/reference-preview.png",
  );
  const bytes = await readFile(sourcePath);
  const image = decodePng(bytes);
  const session = await mkdtemp(path.join(os.tmpdir(), "vsi-audit-reference-"));
  const referenceDirectory = path.join(session, "references", "ref_test");
  const regionsDirectory = path.join(referenceDirectory, "regions");
  await mkdir(regionsDirectory, { recursive: true });
  const regionPath = path.join(regionsDirectory, "full.png");
  await copyFile(sourcePath, regionPath);
  const manifest = {
    reference_id: "ref_test",
    source_path: sourcePath,
    source_sha256: sha256Hex(bytes),
    width_px: image.width,
    height_px: image.height,
    palette: [],
    foreground_bounds: {
      x: 0,
      y: 0,
      width: image.width,
      height: image.height,
    },
    candidate_regions: [{
      id: "page",
      bounds: {
        x: 0,
        y: 0,
        width: image.width,
        height: image.height,
      },
    }],
    candidate_assets: [],
    coordinate_transform: {
      source_origin: "top_left",
      target_origin: "top_left",
      x_axis: "right",
      y_axis: "down",
      units: "pixels",
      matrix: [1, 0, 0, 1, 0, 0],
    },
    pixel_orientation: "top_left_unrotated",
  };
  manifest.manifest_sha256 = computeManifestSha256(manifest);
  const manifestPath = path.join(referenceDirectory, "manifest.json");
  await writeFile(manifestPath, JSON.stringify(manifest), "utf8");

  const result = await loadAuditReference({
    reference_manifest_path: manifestPath,
    reference_region_path: regionPath,
    region_id: "page",
  });

  assert.equal(result.region.id, "page");
  assert.equal(result.region_sha256, sha256Hex(bytes));
});

test("regional visual score is bounded and rewards structural agreement", () => {
  const high = computeRegionalVisualScore({
    edge_f1: 1,
    mae: 0,
    coverage_ratio: 1,
    mean_delta_e76: 0,
  });
  const low = computeRegionalVisualScore({
    edge_f1: 0,
    mae: 1,
    coverage_ratio: 0,
    mean_delta_e76: 100,
  });

  assert.equal(high, 1);
  assert.equal(low, 0);
});
