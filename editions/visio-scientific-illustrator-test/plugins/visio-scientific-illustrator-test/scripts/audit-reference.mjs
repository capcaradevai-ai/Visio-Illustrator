import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";

import {
  computeManifestSha256,
  sha256Hex,
} from "./reference-schema.mjs";
import { decodePng } from "./visual-compare.mjs";

export async function loadAuditReference({
  reference_manifest_path,
  reference_region_path,
  region_id,
}) {
  for (const [name, value] of Object.entries({
    reference_manifest_path,
    reference_region_path,
  })) {
    if (typeof value !== "string" || !path.isAbsolute(value)) {
      throw new Error(`${name} 必须是绝对路径。`);
    }
  }
  const manifestPath = path.resolve(reference_manifest_path);
  const regionPath = path.resolve(reference_region_path);
  const referenceDirectory = path.dirname(manifestPath);
  const relativeRegionPath = path.relative(
    path.join(referenceDirectory, "regions"),
    regionPath,
  );
  if (!relativeRegionPath
      || relativeRegionPath.startsWith("..")
      || path.isAbsolute(relativeRegionPath)) {
    throw new Error("REFERENCE_REGION_OUTSIDE_MANIFEST");
  }

  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  if (computeManifestSha256(manifest) !== manifest.manifest_sha256) {
    throw new Error("REFERENCE_MANIFEST_HASH_MISMATCH");
  }
  if (path.extname(manifest.source_path).toLowerCase() !== ".png") {
    throw new Error("AUDIT_REFERENCE_SOURCE_MUST_BE_PNG");
  }
  const region = manifest.candidate_regions?.find(
    (candidate) => candidate.id === region_id,
  );
  if (!region) throw new Error(`REFERENCE_REGION_NOT_FOUND: ${region_id}`);

  const [sourceBytes, regionBytes] = await Promise.all([
    readFile(manifest.source_path),
    readFile(regionPath),
  ]);
  if (sha256Hex(sourceBytes) !== manifest.source_sha256) {
    throw new Error("REFERENCE_SOURCE_HASH_MISMATCH");
  }
  const sourceImage = decodePng(sourceBytes);
  if (sourceImage.width !== manifest.width_px
      || sourceImage.height !== manifest.height_px) {
    throw new Error("REFERENCE_SOURCE_SIZE_MISMATCH");
  }
  const regionImage = decodePng(regionBytes);
  if (regionImage.width !== region.bounds.width
      || regionImage.height !== region.bounds.height) {
    throw new Error("REFERENCE_REGION_SIZE_MISMATCH");
  }
  return {
    manifest,
    region,
    region_sha256: createHash("sha256").update(regionBytes).digest("hex"),
  };
}

export function computeRegionalVisualScore(metrics) {
  const coverageSimilarity = metrics.coverage_ratio > 0
    ? 1 - Math.min(1, Math.abs(Math.log(metrics.coverage_ratio)))
    : 0;
  const colorSimilarity = 1 - Math.min(1, metrics.mean_delta_e76 / 100);
  return clamp(
    0.55 * metrics.edge_f1
      + 0.25 * (1 - metrics.mae)
      + 0.1 * coverageSimilarity
      + 0.1 * colorSimilarity,
  );
}

function clamp(value) {
  return Math.max(0, Math.min(1, value));
}
