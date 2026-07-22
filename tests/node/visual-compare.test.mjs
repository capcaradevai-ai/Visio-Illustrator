import test from "node:test";
import assert from "node:assert/strict";
import { comparePngBuffers, encodePng } from "../../plugins/visio-scientific-illustrator/scripts/visual-compare.mjs";

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
