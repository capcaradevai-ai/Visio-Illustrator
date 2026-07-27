import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  computeManifestSha256,
  normalizeCandidateRegions,
} from "../../plugins/visio-scientific-illustrator-test/scripts/reference-schema.mjs";
import {
  ReferenceStore,
} from "../../plugins/visio-scientific-illustrator-test/scripts/reference-store.mjs";
import {
  fileTools,
} from "../../plugins/visio-scientific-illustrator-test/scripts/tool-schemas.mjs";
import {
  validateSchema,
} from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";

const fixturePath = fileURLToPath(new URL(
  "../fixtures/reference/simple-reference.png",
  import.meta.url,
));

const expectedToolNames = [
  "visio_test_prepare_reference",
  "visio_test_get_reference_manifest",
  "visio_test_get_reference_region",
  "visio_test_extract_reference_asset",
];

test("reference tools expose the stable Test Edition names and bounded inputs", () => {
  const names = fileTools.map((tool) => tool.name);
  for (const name of expectedToolNames) assert.equal(names.includes(name), true, name);

  const prepare = fileTools.find((tool) => tool.name === "visio_test_prepare_reference");
  assert.doesNotThrow(() => validateSchema({
    source_path: fixturePath,
    candidate_regions: [
      { id: "left", bounds: { x: 0, y: 0, width: 2, height: 4 } },
    ],
  }, prepare.inputSchema));
  assert.throws(() => validateSchema({
    source_path: fixturePath,
    candidate_regions: [
      { id: "left", bounds: { x: 0, y: 0, width: 0, height: 4 } },
    ],
  }, prepare.inputSchema));
});

test("manifest hash excludes manifest_sha256 itself", () => {
  const manifest = {
    reference_id: "ref_1",
    source_path: "C:\\reference.png",
    manifest_sha256: "0".repeat(64),
  };
  const first = computeManifestSha256(manifest);
  const second = computeManifestSha256({
    ...manifest,
    manifest_sha256: "f".repeat(64),
  });

  assert.match(first, /^[0-9a-f]{64}$/);
  assert.equal(first, second);
});

test("candidate regions are normalized deterministically and reject duplicate IDs", () => {
  const first = normalizeCandidateRegions([
    { id: "right", bounds: { x: 2, y: 0, width: 2, height: 4 } },
    { id: "left", bounds: { x: 0, y: 0, width: 2, height: 4 } },
  ], 4, 4);
  const second = normalizeCandidateRegions([...first].reverse(), 4, 4);

  assert.deepEqual(first, second);
  assert.throws(
    () => normalizeCandidateRegions([
      { id: "same", bounds: { x: 0, y: 0, width: 1, height: 1 } },
      { id: "same", bounds: { x: 1, y: 0, width: 1, height: 1 } },
    ], 4, 4),
    (error) => error.bridgeError?.code === "REFERENCE_MANIFEST_INVALID",
  );
});

test("prepare_reference is deterministic and stores all outputs under the session root", async () => {
  const sessionRoot = await fs.mkdtemp(path.join(os.tmpdir(), "vsi-reference-test-"));
  const backend = new FakeReferenceBackend();
  const store = new ReferenceStore({ sessionRoot, backend });
  const command = {
    source_path: fixturePath,
    candidate_regions: [
      { id: "right", bounds: { x: 2, y: 0, width: 2, height: 4 } },
      { id: "left", bounds: { x: 0, y: 0, width: 2, height: 4 } },
    ],
  };

  const first = await store.prepareReference(command);
  const second = await store.prepareReference({
    ...command,
    candidate_regions: [...command.candidate_regions].reverse(),
  });

  assert.equal(first.reference_id, second.reference_id);
  assert.equal(first.manifest_sha256, second.manifest_sha256);
  assert.equal(backend.inspectCalls, 1);
  assert.equal(path.relative(sessionRoot, first.manifest_path).startsWith(".."), false);
  assert.equal(path.relative(sessionRoot, first.manifest_path).includes("references"), true);
  assert.deepEqual(first.coordinate_transform.matrix, [1, 0, 0, 1, 0, 0]);
  assert.deepEqual(first.candidate_assets, []);
});

test("relative paths and unsupported source formats are rejected", async () => {
  const sessionRoot = await fs.mkdtemp(path.join(os.tmpdir(), "vsi-reference-test-"));
  const store = new ReferenceStore({ sessionRoot, backend: new FakeReferenceBackend() });

  await assert.rejects(
    store.prepareReference({ source_path: "relative.png", candidate_regions: [] }),
    (error) => error.bridgeError?.code === "REFERENCE_MANIFEST_INVALID",
  );
  await assert.rejects(
    store.prepareReference({
      source_path: path.join(sessionRoot, "reference.gif"),
      candidate_regions: [],
    }),
    (error) => error.bridgeError?.code === "REFERENCE_MANIFEST_INVALID",
  );
});

test("out-of-bounds regions are rejected with REFERENCE_CROP_INVALID", async () => {
  const sessionRoot = await fs.mkdtemp(path.join(os.tmpdir(), "vsi-reference-test-"));
  const store = new ReferenceStore({ sessionRoot, backend: new FakeReferenceBackend() });

  await assert.rejects(
    store.prepareReference({
      source_path: fixturePath,
      candidate_regions: [
        { id: "outside", bounds: { x: 3, y: 0, width: 2, height: 4 } },
      ],
    }),
    (error) => error.bridgeError?.code === "REFERENCE_CROP_INVALID",
  );
});

test("regions are cropped on demand once and return deterministic metadata", async () => {
  const sessionRoot = await fs.mkdtemp(path.join(os.tmpdir(), "vsi-reference-test-"));
  const backend = new FakeReferenceBackend();
  const store = new ReferenceStore({ sessionRoot, backend });
  const manifest = await store.prepareReference({
    source_path: fixturePath,
    candidate_regions: [
      { id: "left", bounds: { x: 0, y: 0, width: 2, height: 4 } },
    ],
  });

  const first = await store.getReferenceRegion({
    reference_id: manifest.reference_id,
    region_id: "left",
  });
  const second = await store.getReferenceRegion({
    reference_id: manifest.reference_id,
    region_id: "left",
  });

  assert.equal(first.sha256, second.sha256);
  assert.equal(first.local_path, second.local_path);
  assert.equal(backend.cropCalls, 1);
  assert.equal(path.relative(sessionRoot, first.local_path).startsWith(".."), false);
});

test("asset extraction enforces size and blank-ratio limits", async () => {
  const sessionRoot = await fs.mkdtemp(path.join(os.tmpdir(), "vsi-reference-test-"));
  const backend = new FakeReferenceBackend();
  const store = new ReferenceStore({ sessionRoot, backend });
  const manifest = await store.prepareReference({
    source_path: fixturePath,
    candidate_regions: [],
  });

  await assert.rejects(
    store.extractReferenceAsset({
      reference_id: manifest.reference_id,
      bounds: { x: 0, y: 0, width: 4, height: 1 },
      semantic_role: "too_large",
    }),
    (error) => error.bridgeError?.code === "REFERENCE_CROP_INVALID",
  );
  backend.blankRatio = 0.71;
  await assert.rejects(
    store.extractReferenceAsset({
      reference_id: manifest.reference_id,
      bounds: { x: 0, y: 0, width: 1, height: 1 },
      semantic_role: "blank",
    }),
    (error) => error.bridgeError?.code === "REFERENCE_CROP_INVALID",
  );
});

class FakeReferenceBackend {
  inspectCalls = 0;
  cropCalls = 0;
  blankRatio = 0.25;

  async inspectImage() {
    this.inspectCalls += 1;
    return {
      width_px: 4,
      height_px: 4,
      palette: ["#ff0000", "#0000ff"],
      foreground_bounds: { x: 0, y: 0, width: 4, height: 4 },
      pixel_orientation: "top_left_unrotated",
    };
  }

  async cropImage({ output_path, bounds }) {
    this.cropCalls += 1;
    await fs.mkdir(path.dirname(output_path), { recursive: true });
    await fs.writeFile(output_path, Buffer.from(
      `crop:${bounds.x},${bounds.y},${bounds.width},${bounds.height}`,
      "utf8",
    ));
    return {
      output_path,
      width_px: bounds.width,
      height_px: bounds.height,
      sha256: "a".repeat(64),
      blank_ratio: this.blankRatio,
    };
  }
}
