import crypto from "node:crypto";

const referenceIdPattern = /^ref_[0-9a-f]{64}$/;

export class ReferenceContractError extends Error {
  constructor(code, message, details) {
    super(message);
    this.name = "ReferenceContractError";
    this.bridgeError = {
      code,
      message,
      retryable: false,
      ...(details === undefined ? {} : { details }),
    };
  }
}

export function canonicalJson(value) {
  return JSON.stringify(sortValue(value));
}

export function sha256Hex(value) {
  const bytes = Buffer.isBuffer(value) ? value : Buffer.from(value);
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

export function computeManifestSha256(manifest) {
  const { manifest_sha256: _excluded, manifest_path: _path, ...content } = manifest;
  return sha256Hex(canonicalJson(content));
}

export function normalizeCandidateRegions(regions, widthPx, heightPx) {
  if (!Array.isArray(regions)) {
    throw manifestError("candidate_regions must be an array.");
  }
  const normalized = regions.map((region, index) => {
    if (!region || typeof region !== "object" || Array.isArray(region)) {
      throw manifestError(`candidate_regions[${index}] must be an object.`);
    }
    const id = typeof region.id === "string" ? region.id.trim() : "";
    if (!id || id.length > 120) {
      throw manifestError(`candidate_regions[${index}].id is invalid.`);
    }
    return {
      id,
      bounds: normalizePixelBounds(region.bounds, widthPx, heightPx),
    };
  }).sort((left, right) => left.id.localeCompare(right.id, "en"));

  for (let index = 1; index < normalized.length; index += 1) {
    if (normalized[index - 1].id === normalized[index].id) {
      throw manifestError(`Duplicate candidate region id: ${normalized[index].id}`);
    }
  }
  return normalized;
}

export function normalizePixelBounds(bounds, widthPx, heightPx) {
  if (!bounds || typeof bounds !== "object" || Array.isArray(bounds)) {
    throw cropError("bounds must be an object.");
  }
  const normalized = {};
  for (const name of ["x", "y", "width", "height"]) {
    if (!Number.isInteger(bounds[name])) {
      throw cropError(`bounds.${name} must be an integer.`);
    }
    normalized[name] = bounds[name];
  }
  if (normalized.x < 0 || normalized.y < 0
      || normalized.width <= 0 || normalized.height <= 0) {
    throw cropError("bounds must have a non-negative origin and positive size.");
  }
  if (Number.isFinite(widthPx) && Number.isFinite(heightPx)
      && (normalized.x + normalized.width > widthPx
        || normalized.y + normalized.height > heightPx)) {
    throw cropError("bounds exceed the reference image.");
  }
  return normalized;
}

export function validateReferenceId(referenceId) {
  if (!referenceIdPattern.test(referenceId || "")) {
    throw manifestError("reference_id is invalid.");
  }
  return referenceId;
}

export function manifestError(message, details) {
  return new ReferenceContractError("REFERENCE_MANIFEST_INVALID", message, details);
}

export function cropError(message, details) {
  return new ReferenceContractError("REFERENCE_CROP_INVALID", message, details);
}

function sortValue(value) {
  if (Array.isArray(value)) return value.map(sortValue);
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.keys(value)
        .sort((left, right) => left.localeCompare(right, "en"))
        .map((key) => [key, sortValue(value[key])]),
    );
  }
  return value;
}
