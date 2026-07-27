export const qualityProfiles = Object.freeze({
  strict: Object.freeze({
    modelReview: "every_region",
    modelCorrector: "every_failure",
    finalModelReview: true,
  }),
  adaptive: Object.freeze({
    modelReview: "calibration_complexity_failure",
    modelCorrector: "uncertain_or_unsafe",
    finalModelReview: true,
  }),
  economy: Object.freeze({
    modelReview: "failure_or_low_confidence",
    modelCorrector: "uncertain_or_unsafe",
    finalModelReview: true,
  }),
});

export const defaultTaskBudget = Object.freeze({
  quality_profile: "strict",
  max_model_review_rounds_per_region: 1,
  max_full_page_model_reviews: 1,
  return_preview_policy: "failures_only",
  capability_detail_policy: "on_demand",
  auto_apply_safe_patches: true,
});

export function getQualityProfile(name) {
  const profile = qualityProfiles[name];
  if (!profile) throw new Error(`UNKNOWN_QUALITY_PROFILE: ${name}`);
  return profile;
}
