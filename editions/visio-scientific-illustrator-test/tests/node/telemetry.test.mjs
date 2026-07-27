import test from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, readFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { RunTelemetry } from "../../plugins/visio-scientific-illustrator-test/scripts/telemetry.mjs";

test("telemetry estimates only tool response tokens and finalizes immutable local JSON", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "vsi-test-metrics-"));
  const telemetry = new RunTelemetry();
  telemetry.record("visio_test_live_status", { state: "ready" });
  telemetry.record("visio_test_live_audit_figure", {
    hard_count: 0,
    warning_count: 1,
    info_count: 2,
  });

  const result = await telemetry.finalize(root, {
    profile: "strict",
    model_name: "test-model",
    reasoning_level: "medium",
    model_region_review_count: 2,
    model_full_page_review_count: 1,
    structure_score: 1,
    glue_score: 1,
    editability_score: 1,
    visual_score: 0.9,
    text_semantic_accuracy: 1,
  });

  assert.equal(result.complete, false);
  assert.equal(result.estimated, true);
  assert.equal(result.mcp_call_count, 2);
  assert.equal(result.model_region_review_count, 2);
  assert.equal(result.model_full_page_review_count, 1);
  assert.deepEqual(result.finding_counts, { hard: 0, warning: 1, info: 2 });
  const persisted = JSON.parse(await readFile(result.output_path, "utf8"));
  assert.equal(persisted.run_id, result.run_id);
  await assert.rejects(() => telemetry.finalize(root, {
    profile: "strict",
    model_name: "test-model",
    reasoning_level: "medium",
    model_region_review_count: 2,
    model_full_page_review_count: 1,
    structure_score: 1,
    glue_score: 1,
    editability_score: 1,
    visual_score: 0.9,
    text_semantic_accuracy: 1,
  }));
});
