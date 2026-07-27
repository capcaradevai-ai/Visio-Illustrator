import test from "node:test";
import assert from "node:assert/strict";
import {
  compactAuditResult,
  compactFindingList,
} from "../../plugins/visio-scientific-illustrator-test/scripts/audit-response.mjs";

const finding = {
  id: "finding-1",
  severity: "Hard",
  category: "text_overflow",
  region_id: "main",
  object_ids: ["shape-1"],
  observed_revision: 3,
  evidence: { overflow: "true", verbose: "omitted from lists" },
  required_change: "Expand the text box.",
  acceptance_conditions: ["Text must fit."],
  safe_patch: null,
  status: "Open",
};

test("compactAuditResult keeps only hard finding summaries", () => {
  const result = compactAuditResult({
    overall_score: 0.8,
    region_score: 0.75,
    hard_count: 1,
    warning_count: 1,
    info_count: 0,
    findings: [
      finding,
      { ...finding, id: "finding-2", severity: "Warning" },
    ],
    model_review_required: true,
    model_review_reasons: ["low_visual_score"],
    can_pass: false,
    evidence: { page_revision: 3, preview_sha256: "a".repeat(64) },
  });

  assert.equal(result.findings, undefined);
  assert.equal(result.hard_findings.length, 1);
  assert.deepEqual(result.hard_findings[0], {
    id: "finding-1",
    category: "text_overflow",
    object_ids: ["shape-1"],
    required_change: "Expand the text box.",
  });
});

test("compactFindingList omits verbose evidence and correction detail", () => {
  const result = compactFindingList({
    page_revision: 3,
    findings: [finding],
  });

  assert.deepEqual(result, {
    page_revision: 3,
    findings: [{
      id: "finding-1",
      severity: "Hard",
      category: "text_overflow",
      region_id: "main",
      object_ids: ["shape-1"],
      observed_revision: 3,
      status: "Open",
    }],
  });
});
