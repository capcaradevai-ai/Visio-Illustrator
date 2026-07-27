import fs from "node:fs/promises";
import path from "node:path";

import {
  canonicalJson,
  computeManifestSha256,
  cropError,
  manifestError,
  normalizeCandidateRegions,
  normalizePixelBounds,
  sha256Hex,
  validateReferenceId,
} from "./reference-schema.mjs";

const sourceExtensions = new Set([".png", ".jpg", ".jpeg"]);

export class ReferenceStore {
  constructor({ sessionRoot, backend }) {
    if (!path.isAbsolute(sessionRoot || "")) {
      throw new TypeError("sessionRoot must be absolute.");
    }
    if (!backend?.inspectImage || !backend?.cropImage) {
      throw new TypeError("backend must implement inspectImage and cropImage.");
    }
    this.sessionRoot = path.resolve(sessionRoot);
    this.referencesRoot = path.join(this.sessionRoot, "references");
    this.backend = backend;
  }

  async prepareReference({ source_path, candidate_regions = [] }) {
    const sourcePath = await validateSourcePath(source_path);
    const sourceBytes = await readSource(sourcePath);
    const sourceSha256 = sha256Hex(sourceBytes);
    const preliminaryRegions = normalizeCandidateRegions(candidate_regions);
    const referenceId = `ref_${sha256Hex(canonicalJson({
      source_sha256: sourceSha256,
      candidate_regions: preliminaryRegions,
    }))}`;
    const existing = await this.#readManifestIfPresent(referenceId);
    if (existing) return withManifestPath(existing, this.#manifestPath(referenceId));

    const inspection = await this.backend.inspectImage({ source_path: sourcePath });
    validateInspection(inspection);
    const regions = normalizeCandidateRegions(
      preliminaryRegions,
      inspection.width_px,
      inspection.height_px,
    );
    const manifest = {
      reference_id: referenceId,
      source_path: sourcePath,
      source_sha256: sourceSha256,
      width_px: inspection.width_px,
      height_px: inspection.height_px,
      palette: [...inspection.palette],
      foreground_bounds: normalizePixelBounds(
        inspection.foreground_bounds,
        inspection.width_px,
        inspection.height_px,
      ),
      candidate_regions: regions,
      candidate_assets: [],
      coordinate_transform: {
        source_origin: "top_left",
        target_origin: "top_left",
        x_axis: "right",
        y_axis: "down",
        units: "pixels",
        matrix: [1, 0, 0, 1, 0, 0],
      },
      pixel_orientation: inspection.pixel_orientation,
    };
    manifest.manifest_sha256 = computeManifestSha256(manifest);
    const manifestPath = this.#manifestPath(referenceId);
    await writeJsonAtomic(manifestPath, manifest);
    return withManifestPath(manifest, manifestPath);
  }

  async getReferenceManifest({ reference_id }) {
    const referenceId = validateReferenceId(reference_id);
    const manifestPath = this.#manifestPath(referenceId);
    const manifest = await this.#readManifestIfPresent(referenceId);
    if (!manifest) throw manifestError(`Reference not found: ${referenceId}`);
    return withManifestPath(manifest, manifestPath);
  }

  async getReferenceRegion({ reference_id, region_id }) {
    const manifest = await this.getReferenceManifest({ reference_id });
    const regionId = typeof region_id === "string" ? region_id.trim() : "";
    const region = manifest.candidate_regions.find((candidate) => candidate.id === regionId);
    if (!region) throw cropError(`Candidate region not found: ${regionId}`);
    const cropKey = sha256Hex(canonicalJson({
      source_sha256: manifest.source_sha256,
      bounds: region.bounds,
    }));
    const outputPath = path.join(
      this.#referenceDirectory(manifest.reference_id),
      "regions",
      `${cropKey}.png`,
    );
    const existing = await readFileIfPresent(outputPath);
    if (!existing) {
      await this.backend.cropImage({
        source_path: manifest.source_path,
        bounds: region.bounds,
        output_path: outputPath,
      });
    }
    const bytes = await fs.readFile(outputPath);
    return {
      reference_id: manifest.reference_id,
      region_id: region.id,
      local_path: outputPath,
      width_px: region.bounds.width,
      height_px: region.bounds.height,
      bounds: region.bounds,
      sha256: sha256Hex(bytes),
    };
  }

  async extractReferenceAsset({
    reference_id,
    bounds,
    semantic_role,
  }) {
    const manifest = await this.getReferenceManifest({ reference_id });
    const normalizedBounds = normalizePixelBounds(
      bounds,
      manifest.width_px,
      manifest.height_px,
    );
    const semanticRole = typeof semantic_role === "string" ? semantic_role.trim() : "";
    if (!semanticRole || semanticRole.length > 120) {
      throw cropError("semantic_role must be a non-empty string of at most 120 characters.");
    }
    enforceAssetSize(normalizedBounds, manifest.width_px, manifest.height_px);

    const assetDigest = sha256Hex(canonicalJson({
      source_sha256: manifest.source_sha256,
      bounds: normalizedBounds,
      semantic_role: semanticRole,
    }));
    const assetId = `asset_${assetDigest}`;
    const existing = manifest.candidate_assets.find((asset) => asset.asset_id === assetId);
    if (existing) return existing;

    const outputPath = path.join(
      this.#referenceDirectory(manifest.reference_id),
      "assets",
      `${assetDigest}.png`,
    );
    const crop = await this.backend.cropImage({
      source_path: manifest.source_path,
      bounds: normalizedBounds,
      output_path: outputPath,
    });
    if (!Number.isFinite(crop.blank_ratio) || crop.blank_ratio > 0.7) {
      throw cropError("Extracted asset contains more than 70% blank pixels.", {
        blank_ratio: crop.blank_ratio,
      });
    }
    const bytes = await fs.readFile(outputPath);
    const asset = {
      asset_id: assetId,
      local_path: outputPath,
      bounds: normalizedBounds,
      width_px: normalizedBounds.width,
      height_px: normalizedBounds.height,
      sha256: sha256Hex(bytes),
      blank_ratio: crop.blank_ratio,
      semantic_role: semanticRole,
    };
    const storedManifest = stripManifestPath(manifest);
    storedManifest.candidate_assets = [...storedManifest.candidate_assets, asset]
      .sort((left, right) => left.asset_id.localeCompare(right.asset_id, "en"));
    storedManifest.manifest_sha256 = computeManifestSha256(storedManifest);
    await writeJsonAtomic(this.#manifestPath(manifest.reference_id), storedManifest);
    return asset;
  }

  #referenceDirectory(referenceId) {
    return path.join(this.referencesRoot, validateReferenceId(referenceId));
  }

  #manifestPath(referenceId) {
    return path.join(this.#referenceDirectory(referenceId), "manifest.json");
  }

  async #readManifestIfPresent(referenceId) {
    const manifestPath = this.#manifestPath(referenceId);
    try {
      const manifest = JSON.parse(await fs.readFile(manifestPath, "utf8"));
      if (manifest.reference_id !== referenceId
          || computeManifestSha256(manifest) !== manifest.manifest_sha256) {
        throw manifestError(`Stored manifest is invalid: ${referenceId}`);
      }
      return manifest;
    } catch (error) {
      if (error?.code === "ENOENT") return null;
      if (error instanceof SyntaxError) {
        throw manifestError(`Stored manifest JSON is invalid: ${referenceId}`);
      }
      throw error;
    }
  }
}

function enforceAssetSize(bounds, widthPx, heightPx) {
  const areaRatio = (bounds.width * bounds.height) / (widthPx * heightPx);
  if (areaRatio > 0.2) {
    throw cropError("Extracted asset area exceeds 20% of the reference image.");
  }
  if (bounds.width / widthPx > 0.6 || bounds.height / heightPx > 0.6) {
    throw cropError("Extracted asset width or height exceeds 60% of the reference image.");
  }
}

async function validateSourcePath(sourcePath) {
  if (typeof sourcePath !== "string" || !path.isAbsolute(sourcePath)) {
    throw manifestError("source_path must be an absolute local path.");
  }
  const normalized = path.resolve(sourcePath);
  if (!sourceExtensions.has(path.extname(normalized).toLowerCase())) {
    throw manifestError("Only local PNG and JPEG reference images are supported.");
  }
  return normalized;
}

async function readSource(sourcePath) {
  try {
    const stats = await fs.stat(sourcePath);
    if (!stats.isFile()) throw manifestError("source_path must identify a file.");
    return await fs.readFile(sourcePath);
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw manifestError(`Reference source does not exist: ${sourcePath}`);
    }
    throw error;
  }
}

function validateInspection(inspection) {
  if (!inspection || !Number.isInteger(inspection.width_px)
      || !Number.isInteger(inspection.height_px)
      || inspection.width_px <= 0 || inspection.height_px <= 0
      || !Array.isArray(inspection.palette)
      || inspection.palette.length > 12
      || inspection.pixel_orientation !== "top_left_unrotated") {
    throw manifestError("Bridge returned invalid reference image inspection metadata.");
  }
}

async function writeJsonAtomic(outputPath, value) {
  await fs.mkdir(path.dirname(outputPath), { recursive: true });
  const candidate = `${outputPath}.${process.pid}.tmp`;
  await fs.writeFile(candidate, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  await fs.rename(candidate, outputPath);
}

async function readFileIfPresent(filePath) {
  try {
    return await fs.readFile(filePath);
  } catch (error) {
    if (error?.code === "ENOENT") return null;
    throw error;
  }
}

function withManifestPath(manifest, manifestPath) {
  return { ...manifest, manifest_path: manifestPath };
}

function stripManifestPath(manifest) {
  const { manifest_path: _path, ...stored } = manifest;
  return stored;
}
