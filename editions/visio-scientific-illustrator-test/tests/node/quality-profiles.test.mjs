import assert from "node:assert/strict";
import test from "node:test";
import {
  defaultTaskBudget,
  qualityProfiles,
} from "../../plugins/visio-scientific-illustrator-test/scripts/quality-profiles.mjs";
import {
  computeRegionComplexity,
  evaluateReviewPolicy,
} from "../../plugins/visio-scientific-illustrator-test/scripts/review-policy.mjs";

test("quality profiles retain the approved model review policies", () => {
  assert.equal(qualityProfiles.strict.modelReview, "every_region");
  assert.equal(
    qualityProfiles.adaptive.modelReview,
    "calibration_complexity_failure",
  );
  assert.equal(
    qualityProfiles.economy.modelReview,
    "failure_or_low_confidence",
  );
  assert.ok(Object.values(qualityProfiles).every((profile) => profile.finalModelReview));
  assert.equal(defaultTaskBudget.quality_profile, "strict");
  assert.equal(defaultTaskBudget.max_model_review_rounds_per_region, 1);
  assert.equal(defaultTaskBudget.max_full_page_model_reviews, 1);
  assert.equal(defaultTaskBudget.return_preview_policy, "failures_only");
});

test("adaptive requires first-region calibration and most-complex review once", () => {
  const firstAndMostComplex = evaluateReviewPolicy({
    profile: "adaptive",
    regionId: "left",
    regionIndex: 0,
    mostComplexRegionId: "left",
  });
  assert.equal(firstAndMostComplex.model_review_required, true);
  assert.deepEqual(firstAndMostComplex.reasons.sort(), [
    "first_region_calibration",
    "most_complex_region",
  ]);
  assert.equal(firstAndMostComplex.model_review_count, 1);
});

test("adaptive escalates uncertainty and never silently passes", () => {
  const result = evaluateReviewPolicy({
    profile: "adaptive",
    regionId: "right",
    regionIndex: 1,
    mostComplexRegionId: "left",
    hardFindingCount: 1,
    semanticConfidence: 0.6,
    representationConfidence: 0.5,
    safePatchFailed: true,
  });
  assert.equal(result.model_review_required, true);
  assert.deepEqual(result.reasons.sort(), [
    "hard_finding",
    "low_representation_confidence",
    "low_semantic_confidence",
    "safe_patch_failed",
  ]);
});

test("region complexity is deterministic and exposes weighted factors", () => {
  const metrics = {
    object_count: 12,
    connector_count: 4,
    connector_crossing_count: 1,
    text_density: 0.3,
    composite_component_count: 2,
    raster_asset_count: 1,
    overlap_density: 0.1,
    visual_metric_uncertainty: 0.2,
  };
  const first = computeRegionComplexity(metrics);
  const second = computeRegionComplexity({ ...metrics });
  assert.deepEqual(first, second);
  assert.ok(first.score > 0);
  assert.equal(Object.keys(first.factors).length, 8);
});
