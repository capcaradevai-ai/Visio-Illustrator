import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { comparePngBuffers, comparePngFiles, encodePng } from "../../plugins/visio-scientific-illustrator/scripts/visual-compare.mjs";

function image(width, height, painter) {
  const pixels = new Uint8Array(width * height * 4).fill(255);
  painter?.(pixels, width, height);
  return encodePng(width, height, pixels);
}

function setPixel(pixels, width, x, y, color) {
  const offset = (y * width + x) * 4;
  pixels.set([...color, 255], offset);
}

test("identical PNGs have perfect structural metrics", () => {
  const png = image(40, 40, (pixels, width) => {
    for (let y = 8; y < 32; y += 1) for (let x = 8; x < 32; x += 1) setPixel(pixels, width, x, y, [30, 80, 140]);
  });
  const metrics = comparePngBuffers(png, png);
  assert.equal(metrics.mae, 0);
  assert.equal(metrics.edge_f1, 1);
  assert.equal(metrics.coverage_ratio, 1);
  assert.equal(metrics.mean_delta_e76, 0);
});

test("white background cannot hide missing scientific structure", () => {
  const reference = image(100, 100, (pixels, width) => {
    for (let x = 20; x < 80; x += 1) setPixel(pixels, width, x, 50, [0, 0, 0]);
  });
  const blank = image(100, 100);
  const metrics = comparePngBuffers(reference, blank);
  assert.ok(metrics.mae < 0.02, "global pixel error remains deceptively small");
  assert.equal(metrics.edge_recall, 0);
  assert.equal(metrics.coverage_ratio, 0);
});

test("small translation is tolerated more than deletion and color shift is reported", () => {
  const square = (shift, color) => image(60, 60, (pixels, width) => {
    for (let y = 15; y < 40; y += 1) for (let x = 15 + shift; x < 40 + shift; x += 1) setPixel(pixels, width, x, y, color);
  });
  const reference = square(0, [30, 90, 160]);
  const shifted = comparePngBuffers(reference, square(1, [30, 90, 160]));
  const recolored = comparePngBuffers(reference, square(0, [180, 90, 30]));
  assert.ok(shifted.edge_f1 > 0.9);
  assert.ok(recolored.mean_delta_e76 > 20);
});

test("logical regions are measured independently from the global canvas", async () => {
  const reference = image(80, 40, (pixels, width) => {
    for (let y = 5; y < 35; y += 1) for (let x = 5; x < 35; x += 1) setPixel(pixels, width, x, y, [20, 80, 150]);
    for (let y = 5; y < 35; y += 1) for (let x = 45; x < 75; x += 1) setPixel(pixels, width, x, y, [170, 80, 20]);
  });
  const candidate = image(80, 40, (pixels, width) => {
    for (let y = 5; y < 35; y += 1) for (let x = 5; x < 35; x += 1) setPixel(pixels, width, x, y, [20, 80, 150]);
  });
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "visio-regions-"));
  try {
    const referencePath = path.join(directory, "reference.png");
    const candidatePath = path.join(directory, "candidate.png");
    await Promise.all([fs.writeFile(referencePath, reference), fs.writeFile(candidatePath, candidate)]);
    const metrics = await comparePngFiles(referencePath, candidatePath, null, [
      { id: "left", x: 0, y: 0, width: 40, height: 40 },
      { id: "right", x: 40, y: 0, width: 40, height: 40 },
    ]);
    assert.equal(metrics.regions[0].edge_f1, 1);
    assert.equal(metrics.regions[1].edge_recall, 0);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});

test("file comparison normalizes renderer dimensions and reports the original canvas error", async () => {
  const reference = image(80, 40, (pixels, width) => {
    for (let y = 10; y < 30; y += 1) for (let x = 20; x < 60; x += 1) setPixel(pixels, width, x, y, [20, 80, 150]);
  });
  const candidate = image(160, 80, (pixels, width) => {
    for (let y = 20; y < 60; y += 1) for (let x = 40; x < 120; x += 1) setPixel(pixels, width, x, y, [20, 80, 150]);
  });
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "visio-resize-"));
  try {
    const referencePath = path.join(directory, "reference.png");
    const candidatePath = path.join(directory, "candidate.png");
    await Promise.all([fs.writeFile(referencePath, reference), fs.writeFile(candidatePath, candidate)]);
    const metrics = await comparePngFiles(referencePath, candidatePath);
    assert.equal(metrics.canvas.normalized, true);
    assert.equal(metrics.canvas.candidate_width, 160);
    assert.equal(metrics.canvas.aspect_ratio_error, 0);
    assert.ok(metrics.edge_f1 > 0.9);
  } finally {
    await fs.rm(directory, { recursive: true, force: true });
  }
});
