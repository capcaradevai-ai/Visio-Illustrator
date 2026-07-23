import fs from "node:fs/promises";
import zlib from "node:zlib";

export async function comparePngFiles(referencePath, candidatePath, heatmapPath, regions = []) {
  const [referenceBuffer, candidateBuffer] = await Promise.all([
    fs.readFile(referencePath),
    fs.readFile(candidatePath),
  ]);
  const reference = decodePng(referenceBuffer);
  const rawCandidate = decodePng(candidateBuffer);
  const normalized = reference.width !== rawCandidate.width || reference.height !== rawCandidate.height;
  const candidate = normalized
    ? resizeImage(rawCandidate, reference.width, reference.height)
    : rawCandidate;
  const normalizedCandidateBuffer = normalized
    ? encodePng(candidate.width, candidate.height, candidate.data)
    : candidateBuffer;
  const result = comparePngBuffers(referenceBuffer, normalizedCandidateBuffer);
  if (heatmapPath) {
    await fs.writeFile(heatmapPath, encodePng(result.width, result.height, result.heatmap));
  }
  const regionMetrics = regions.map((region) => {
    validateRegion(region, reference.width, reference.height);
    const referenceCrop = crop(reference, region);
    const candidateCrop = crop(candidate, region);
    const compared = comparePngBuffers(
      encodePng(region.width, region.height, referenceCrop.data),
      encodePng(region.width, region.height, candidateCrop.data),
    );
    const { heatmap: regionHeatmap, ...values } = compared;
    return { id: region.id, bounds: { x: region.x, y: region.y, width: region.width, height: region.height }, ...values };
  });
  const { heatmap, ...metrics } = result;
  return {
    canvas: {
      reference_width: reference.width,
      reference_height: reference.height,
      candidate_width: rawCandidate.width,
      candidate_height: rawCandidate.height,
      aspect_ratio_error: Math.abs(
        reference.width / reference.height - rawCandidate.width / rawCandidate.height,
      ) / (reference.width / reference.height),
      normalized,
    },
    ...metrics,
    regions: regionMetrics,
    heatmap_path: heatmapPath ?? null,
  };
}

function resizeImage(image, width, height) {
  const data = new Uint8Array(width * height * 4);
  for (let y = 0; y < height; y += 1) {
    const sourceY = Math.min(image.height - 1, Math.max(0, (y + 0.5) * image.height / height - 0.5));
    const y0 = Math.floor(sourceY);
    const y1 = Math.min(image.height - 1, y0 + 1);
    const wy = sourceY - y0;
    for (let x = 0; x < width; x += 1) {
      const sourceX = Math.min(image.width - 1, Math.max(0, (x + 0.5) * image.width / width - 0.5));
      const x0 = Math.floor(sourceX);
      const x1 = Math.min(image.width - 1, x0 + 1);
      const wx = sourceX - x0;
      const target = (y * width + x) * 4;
      for (let channel = 0; channel < 4; channel += 1) {
        const top = image.data[(y0 * image.width + x0) * 4 + channel] * (1 - wx)
          + image.data[(y0 * image.width + x1) * 4 + channel] * wx;
        const bottom = image.data[(y1 * image.width + x0) * 4 + channel] * (1 - wx)
          + image.data[(y1 * image.width + x1) * 4 + channel] * wx;
        data[target + channel] = Math.round(top * (1 - wy) + bottom * wy);
      }
    }
  }
  return { width, height, data };
}

export function comparePngBuffers(referenceBuffer, candidateBuffer) {
  const reference = decodePng(referenceBuffer);
  const candidate = decodePng(candidateBuffer);
  if (reference.width !== candidate.width || reference.height !== candidate.height) {
    throw new Error(`PNG_SIZE_MISMATCH: ${reference.width}x${reference.height} vs ${candidate.width}x${candidate.height}`);
  }
  const pixelCount = reference.width * reference.height;
  let absolute = 0;
  let referenceInk = 0;
  let candidateInk = 0;
  let colorDelta = 0;
  let colorSamples = 0;
  const heatmap = new Uint8Array(pixelCount * 4);
  for (let pixel = 0; pixel < pixelCount; pixel += 1) {
    const offset = pixel * 4;
    const dr = Math.abs(reference.data[offset] - candidate.data[offset]);
    const dg = Math.abs(reference.data[offset + 1] - candidate.data[offset + 1]);
    const db = Math.abs(reference.data[offset + 2] - candidate.data[offset + 2]);
    absolute += dr + dg + db;
    const magnitude = Math.min(255, Math.round((dr + dg + db) / 3 * 2));
    heatmap[offset] = magnitude;
    heatmap[offset + 1] = Math.max(0, 80 - magnitude);
    heatmap[offset + 2] = 0;
    heatmap[offset + 3] = 255;
    const refNonWhite = reference.data[offset] < 248 || reference.data[offset + 1] < 248 || reference.data[offset + 2] < 248;
    const candNonWhite = candidate.data[offset] < 248 || candidate.data[offset + 1] < 248 || candidate.data[offset + 2] < 248;
    if (refNonWhite) referenceInk += 1;
    if (candNonWhite) candidateInk += 1;
    if (refNonWhite || candNonWhite) {
      colorDelta += deltaE76(
        rgbToLab(reference.data[offset], reference.data[offset + 1], reference.data[offset + 2]),
        rgbToLab(candidate.data[offset], candidate.data[offset + 1], candidate.data[offset + 2]),
      );
      colorSamples += 1;
    }
  }
  const referenceEdges = edgeMask(reference);
  const candidateEdges = edgeMask(candidate);
  let truePositive = 0;
  let referenceEdgeCount = 0;
  let candidateEdgeCount = 0;
  for (let index = 0; index < referenceEdges.length; index += 1) {
    if (referenceEdges[index]) referenceEdgeCount += 1;
    if (candidateEdges[index]) candidateEdgeCount += 1;
    if (referenceEdges[index] && hasNeighbor(candidateEdges, reference.width, reference.height, index, 1)) truePositive += 1;
  }
  const precisionHits = countMatched(candidateEdges, referenceEdges, reference.width, reference.height);
  const precision = candidateEdgeCount === 0 ? (referenceEdgeCount === 0 ? 1 : 0) : precisionHits / candidateEdgeCount;
  const recall = referenceEdgeCount === 0 ? 1 : truePositive / referenceEdgeCount;
  const f1 = precision + recall === 0 ? 0 : 2 * precision * recall / (precision + recall);
  return {
    width: reference.width,
    height: reference.height,
    mae: absolute / (pixelCount * 3 * 255),
    edge_precision: precision,
    edge_recall: recall,
    edge_f1: f1,
    reference_coverage: referenceInk / pixelCount,
    candidate_coverage: candidateInk / pixelCount,
    coverage_ratio: referenceInk === 0 ? (candidateInk === 0 ? 1 : 0) : candidateInk / referenceInk,
    mean_delta_e76: colorSamples === 0 ? 0 : colorDelta / colorSamples,
    heatmap,
  };
}

export function encodePng(width, height, rgba) {
  if (rgba.length !== width * height * 4) throw new Error("RGBA_LENGTH_MISMATCH");
  const scanlines = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y += 1) {
    const target = y * (width * 4 + 1);
    scanlines[target] = 0;
    Buffer.from(rgba.buffer, rgba.byteOffset + y * width * 4, width * 4).copy(scanlines, target + 1);
  }
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0); ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  return Buffer.concat([signature, chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(scanlines)), chunk("IEND", Buffer.alloc(0))]);
}

function validateRegion(region, width, height) {
  if (!region || typeof region.id !== "string" || region.id.length === 0
      || !Number.isInteger(region.x) || !Number.isInteger(region.y)
      || !Number.isInteger(region.width) || !Number.isInteger(region.height)
      || region.x < 0 || region.y < 0 || region.width <= 0 || region.height <= 0
      || region.x + region.width > width || region.y + region.height > height) {
    throw new Error(`INVALID_COMPARE_REGION: ${region?.id ?? "unknown"}`);
  }
}

function crop(image, region) {
  const data = new Uint8Array(region.width * region.height * 4);
  for (let y = 0; y < region.height; y += 1) {
    const source = ((region.y + y) * image.width + region.x) * 4;
    const target = y * region.width * 4;
    data.set(image.data.subarray(source, source + region.width * 4), target);
  }
  return { width: region.width, height: region.height, data };
}

export function decodePng(buffer) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (buffer.length < 33 || !buffer.subarray(0, 8).equals(signature)) throw new Error("INVALID_PNG");
  let offset = 8;
  let width; let height; let colorType; let bitDepth;
  const compressed = [];
  while (offset + 12 <= buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString("ascii", offset + 4, offset + 8);
    const data = buffer.subarray(offset + 8, offset + 8 + length);
    if (type === "IHDR") {
      width = data.readUInt32BE(0); height = data.readUInt32BE(4);
      bitDepth = data[8]; colorType = data[9];
    } else if (type === "IDAT") compressed.push(data);
    else if (type === "IEND") break;
    offset += length + 12;
  }
  if (!width || !height || bitDepth !== 8 || ![0, 2, 4, 6].includes(colorType)) throw new Error("UNSUPPORTED_PNG_FORMAT");
  const channels = { 0: 1, 2: 3, 4: 2, 6: 4 }[colorType];
  const stride = width * channels;
  const inflated = zlib.inflateSync(Buffer.concat(compressed));
  if (inflated.length !== (stride + 1) * height) throw new Error("INVALID_PNG_SCANLINES");
  const raw = Buffer.alloc(stride * height);
  for (let y = 0; y < height; y += 1) {
    const source = y * (stride + 1);
    const filter = inflated[source];
    for (let x = 0; x < stride; x += 1) {
      const value = inflated[source + 1 + x];
      const left = x >= channels ? raw[y * stride + x - channels] : 0;
      const up = y > 0 ? raw[(y - 1) * stride + x] : 0;
      const upLeft = y > 0 && x >= channels ? raw[(y - 1) * stride + x - channels] : 0;
      raw[y * stride + x] = unfilter(filter, value, left, up, upLeft);
    }
  }
  const rgba = new Uint8Array(width * height * 4);
  for (let pixel = 0; pixel < width * height; pixel += 1) {
    const source = pixel * channels; const target = pixel * 4;
    if (colorType === 0) rgba.set([raw[source], raw[source], raw[source], 255], target);
    if (colorType === 2) rgba.set([raw[source], raw[source + 1], raw[source + 2], 255], target);
    if (colorType === 4) rgba.set([raw[source], raw[source], raw[source], raw[source + 1]], target);
    if (colorType === 6) rgba.set(raw.subarray(source, source + 4), target);
  }
  return { width, height, data: rgba };
}

function unfilter(filter, value, left, up, upLeft) {
  if (filter === 0) return value;
  if (filter === 1) return (value + left) & 255;
  if (filter === 2) return (value + up) & 255;
  if (filter === 3) return (value + Math.floor((left + up) / 2)) & 255;
  if (filter === 4) return (value + paeth(left, up, upLeft)) & 255;
  throw new Error(`UNSUPPORTED_PNG_FILTER_${filter}`);
}

function paeth(a, b, c) {
  const p = a + b - c; const pa = Math.abs(p - a); const pb = Math.abs(p - b); const pc = Math.abs(p - c);
  return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
}

function edgeMask(image) {
  const gray = new Float32Array(image.width * image.height);
  for (let pixel = 0; pixel < gray.length; pixel += 1) {
    const offset = pixel * 4;
    gray[pixel] = image.data[offset] * .2126 + image.data[offset + 1] * .7152 + image.data[offset + 2] * .0722;
  }
  const mask = new Uint8Array(gray.length);
  for (let y = 1; y < image.height - 1; y += 1) {
    for (let x = 1; x < image.width - 1; x += 1) {
      const at = (dx, dy) => gray[(y + dy) * image.width + x + dx];
      const gx = -at(-1, -1) + at(1, -1) - 2 * at(-1, 0) + 2 * at(1, 0) - at(-1, 1) + at(1, 1);
      const gy = -at(-1, -1) - 2 * at(0, -1) - at(1, -1) + at(-1, 1) + 2 * at(0, 1) + at(1, 1);
      if (Math.hypot(gx, gy) >= 80) mask[y * image.width + x] = 1;
    }
  }
  return mask;
}

function hasNeighbor(mask, width, height, index, radius) {
  const x = index % width; const y = Math.floor(index / width);
  for (let dy = -radius; dy <= radius; dy += 1) for (let dx = -radius; dx <= radius; dx += 1) {
    const nx = x + dx; const ny = y + dy;
    if (nx >= 0 && nx < width && ny >= 0 && ny < height && mask[ny * width + nx]) return true;
  }
  return false;
}

function countMatched(source, target, width, height) {
  let count = 0;
  for (let index = 0; index < source.length; index += 1) if (source[index] && hasNeighbor(target, width, height, index, 1)) count += 1;
  return count;
}

function rgbToLab(r, g, b) {
  const linear = (value) => {
    const normalized = value / 255;
    return normalized <= .04045 ? normalized / 12.92 : ((normalized + .055) / 1.055) ** 2.4;
  };
  const red = linear(r); const green = linear(g); const blue = linear(b);
  const x = (red * .4124 + green * .3576 + blue * .1805) / .95047;
  const y = red * .2126 + green * .7152 + blue * .0722;
  const z = (red * .0193 + green * .1192 + blue * .9505) / 1.08883;
  const f = (value) => value > .008856 ? Math.cbrt(value) : 7.787 * value + 16 / 116;
  return [116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))];
}

function deltaE76(left, right) {
  return Math.hypot(left[0] - right[0], left[1] - right[1], left[2] - right[2]);
}

function chunk(type, data) {
  const typeBuffer = Buffer.from(type, "ascii");
  const output = Buffer.alloc(data.length + 12);
  output.writeUInt32BE(data.length, 0); typeBuffer.copy(output, 4); data.copy(output, 8);
  output.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), data.length + 8);
  return output;
}

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0);
  }
  return (crc ^ 0xffffffff) >>> 0;
}
