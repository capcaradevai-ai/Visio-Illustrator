import assert from "node:assert/strict";
import test from "node:test";
import { mapToolError } from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";

const qualityErrorCodes = [
  "QUALITY_GATE_BLOCKED",
  "STALE_AUDIT_EVIDENCE",
  "REVISION_CONFLICT",
  "RASTER_DECLARATION_INVALID",
  "COMPONENT_CONTRACT_INVALID",
  "REFERENCE_CROP_INVALID",
  "CAPABILITY_NOT_AVAILABLE",
  "REFERENCE_MANIFEST_INVALID",
  "ASSET_PROVENANCE_INVALID",
  "AUDIT_STATE_CORRUPT",
];

test("quality errors retain their stable bridge codes", () => {
  for (const code of qualityErrorCodes) {
    const error = new Error(code);
    error.bridgeError = {
      code,
      message: code,
      retryable: code === "STALE_AUDIT_EVIDENCE",
      details: { region_id: "left", page_revision: 3 },
    };
    assert.deepEqual(mapToolError(error, "visio_test_live_audit_figure"), {
      error: code,
      message: code,
      retryable: code === "STALE_AUDIT_EVIDENCE",
      details: { region_id: "left", page_revision: 3 },
      tool: "visio_test_live_audit_figure",
    });
  }
});
