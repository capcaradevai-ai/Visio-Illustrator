#!/usr/bin/env node

import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  defaultTaskBudget,
  getQualityProfile,
} from "../plugins/visio-scientific-illustrator-test/scripts/quality-profiles.mjs";
import {
  computeRegionComplexity,
  evaluateReviewPolicy,
} from "../plugins/visio-scientific-illustrator-test/scripts/review-policy.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

export async function runSimpleQualityLoop(stepDelayMs = 50) {
  if (!Number.isInteger(stepDelayMs) || stepDelayMs < 0 || stepDelayMs > 10000) {
    throw new Error("step-delay-ms 必须是 0 到 10000 的整数。");
  }
  const scene = JSON.parse(await readFile(
    path.join(root, "tests/fixtures/scientific-scene/reference-scene.json"),
    "utf8",
  ));
  const regions = scene.regions.slice(0, 2);
  const complexity = regions.map((region, index) => ({
    regionId: region.id,
    metrics: computeRegionComplexity({
      object_count: scene.nodes.filter((node) => {
        const centerX = node.bounds.x + node.bounds.width / 2;
        const centerY = node.bounds.y + node.bounds.height / 2;
        return centerX >= region.bounds.x
          && centerX <= region.bounds.x + region.bounds.width
          && centerY >= region.bounds.y
          && centerY <= region.bounds.y + region.bounds.height;
      }).length,
      connector_count: Math.ceil(scene.connectors.length / regions.length),
      connector_crossing_count: 0,
      text_density: 0.2,
      composite_component_count: 1,
      raster_asset_count: 0,
      overlap_density: 0,
      visual_metric_uncertainty: index === 0 ? 0.1 : 0.2,
    }),
  }));
  const mostComplex = [...complexity]
    .sort((left, right) => right.metrics.score - left.metrics.score)[0]?.regionId;
  const profile = getQualityProfile(defaultTaskBudget.quality_profile);
  const checkpoints = [];
  for (let index = 0; index < complexity.length; index += 1) {
    const region = complexity[index];
    const review = evaluateReviewPolicy({
      profile: defaultTaskBudget.quality_profile,
      regionId: region.regionId,
      regionIndex: index,
      mostComplexRegionId: mostComplex,
    });
    checkpoints.push({
      region_id: region.regionId,
      state: "passed",
      model_review_required: review.model_review_required,
    });
    if (stepDelayMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, stepDelayMs));
    }
  }
  return {
    profile: defaultTaskBudget.quality_profile,
    model_review_policy: profile.modelReview,
    region_count: checkpoints.length,
    checkpoints,
    passed: checkpoints.length > 0
      && checkpoints.every((checkpoint) => checkpoint.state === "passed"),
  };
}

const delayOption = process.argv.indexOf("--step-delay-ms");
const stepDelayMs = delayOption >= 0
  ? Number(process.argv[delayOption + 1])
  : 50;
runSimpleQualityLoop(stepDelayMs)
  .then((result) => {
    process.stdout.write(`${JSON.stringify(result)}\n`);
  })
  .catch((error) => {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  });
