import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, readFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { compare, prepare } from "../../scripts/run-quality-ab.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));

test("A/B prepare fixes strict-before-adaptive and incomplete comparison keeps strict", async () => {
  const output = await mkdtemp(path.join(os.tmpdir(), "vsi-quality-ab-"));
  const config = path.resolve(
    here,
    "../fixtures/simple-quality-loop/ab-config.json",
  );
  const prepared = await prepare(config, output);
  assert.deepEqual(
    prepared.manifest.runs.map((run) => run.profile),
    ["strict", "adaptive"],
  );
  assert.equal(prepared.manifest.common.maximum_regions, 2);

  const compared = await compare(prepared.manifest_path);
  assert.equal(compared.summary.status, "incomplete");
  assert.equal(compared.summary.default_profile, "strict");
  const report = await readFile(compared.report_path, "utf8");
  assert.match(report, /Default profile: strict/);
});
