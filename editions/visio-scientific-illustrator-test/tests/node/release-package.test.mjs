import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const editionRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const repositoryRoot = path.resolve(editionRoot, "../..");

test("Codex and Claude Test installers target only isolated IDs", async () => {
  for (const name of ["install-test.ps1", "install-claude-test.ps1"]) {
    const source = await readFile(path.join(repositoryRoot, name), "utf8");
    assert.match(source, /visio-scientific-tools-test/);
    assert.match(source, /visio-scientific-illustrator-test/);
    assert.doesNotMatch(source, /plugin\s+(uninstall|remove).*visio-scientific-illustrator@/i);
    assert.match(source, /verify-stable-hashes\.mjs/);
  }
});

test("bilingual Test README documents both platforms and strict default", async () => {
  const chinese = await readFile(path.join(repositoryRoot, "README.test.md"), "utf8");
  const english = await readFile(path.join(repositoryRoot, "README.test.en.md"), "utf8");
  for (const source of [chinese, english]) {
    assert.match(source, /install-test\.ps1/);
    assert.match(source, /install-claude-test\.ps1/);
    assert.match(source, /strict/);
    assert.match(source, /adaptive/);
    assert.match(source, /economy/);
    assert.match(source, /500 ms/);
    assert.match(source, /2000 px/);
    assert.match(source, /atomic_raster_unit/);
    assert.match(source, /QUALITY_GATE_BLOCKED/);
  }
});

test("new release text is strict UTF-8 without replacement characters", async () => {
  const files = [
    "README.test.md",
    "README.test.en.md",
    "install-test.ps1",
    "install-claude-test.ps1",
  ];
  for (const relative of files) {
    const source = await readFile(path.join(repositoryRoot, relative), {
      encoding: "utf8",
    });
    assert.doesNotMatch(source, /\uFFFD/);
  }
});
