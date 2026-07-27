import { getQualityProfile } from "./quality-profiles.mjs";

const confidenceThreshold = 0.75;
const complexityWeights = Object.freeze({
  object_count: 1,
  connector_count: 2,
  connector_crossing_count: 5,
  text_density: 10,
  composite_component_count: 4,
  raster_asset_count: 5,
  overlap_density: 12,
  visual_metric_uncertainty: 12,
});

export function computeRegionComplexity(metrics) {
  const factors = {};
  let score = 0;
  for (const [name, weight] of Object.entries(complexityWeights)) {
    const value = metrics[name];
    if (typeof value !== "number" || !Number.isFinite(value) || value < 0) {
      throw new Error(`INVALID_COMPLEXITY_METRIC: ${name}`);
    }
    const weighted = Math.round(value * weight * 10_000) / 10_000;
    factors[name] = weighted;
    score += weighted;
  }
  return {
    score: Math.round(score * 10_000) / 10_000,
    factors,
  };
}

export function evaluateReviewPolicy({
  profile,
  regionId,
  regionIndex,
  mostComplexRegionId,
  hardFindingCount = 0,
  visualMetricBelowThreshold = false,
  semanticConfidence = 1,
  connectorComplexity = false,
  rasterAssetCount = 0,
  compositeComplexity = false,
  safePatchFailed = false,
  representationConfidence = 1,
}) {
  getQualityProfile(profile);
  const reasons = [];

  if (profile === "strict") reasons.push("strict_every_region");
  if (profile === "adaptive" && regionIndex === 0) {
    reasons.push("first_region_calibration");
  }
  if (
    profile === "adaptive" &&
    regionId === mostComplexRegionId
  ) {
    reasons.push("most_complex_region");
  }
  if (hardFindingCount > 0) reasons.push("hard_finding");
  if (visualMetricBelowThreshold) reasons.push("low_visual_metric");
  if (semanticConfidence < confidenceThreshold) {
    reasons.push("low_semantic_confidence");
  }
  if (connectorComplexity) reasons.push("complex_connector_topology");
  if (rasterAssetCount > 1) reasons.push("multiple_raster_assets");
  if (compositeComplexity) reasons.push("complex_composite_component");
  if (safePatchFailed) reasons.push("safe_patch_failed");
  if (representationConfidence < confidenceThreshold) {
    reasons.push("low_representation_confidence");
  }

  const uniqueReasons = [...new Set(reasons)];
  return {
    model_review_required: uniqueReasons.length > 0,
    reasons: uniqueReasons,
    model_review_count: uniqueReasons.length > 0 ? 1 : 0,
  };
}
