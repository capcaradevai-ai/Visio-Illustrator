import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import {
  existsSync,
  mkdtempSync,
  readFileSync,
  rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const SOURCE_COMMIT = "69df39c641f38574110563ee343f2a4d8fad30a3";
const protectedRoots = [
  "plugins/visio-scientific-illustrator",
  ".agents/plugins/marketplace.json",
  ".claude-plugin/marketplace.json",
  "install.ps1",
  "install-claude.ps1",
];

const root = fileURLToPath(new URL("../../../../", import.meta.url));
const captureScriptUrl = new URL("../../scripts/capture-stable-hashes.mjs", import.meta.url);
const verifyScriptUrl = new URL("../../scripts/verify-stable-hashes.mjs", import.meta.url);
const manifestUrl = new URL("../../stable-protected.sha256.json", import.meta.url);

const requiredFilesExist = [
  captureScriptUrl,
  verifyScriptUrl,
  manifestUrl,
].every((url) => existsSync(url));

test("稳定版保护脚本和基线清单存在", () => {
  assert.ok(existsSync(captureScriptUrl), "缺少稳定版哈希捕获脚本");
  assert.ok(existsSync(verifyScriptUrl), "缺少稳定版哈希校验脚本");
  assert.ok(existsSync(manifestUrl), "缺少稳定版哈希基线清单");
});

if (existsSync(captureScriptUrl)) {
  test("捕获以 Git 差异确认工作区并对来源提交原始字节计算哈希", async () => {
    const { captureStableHashes } = await import(captureScriptUrl);
    const temporaryDirectory = mkdtempSync(
      join(tmpdir(), "visio-stable-hash-test-"),
    );
    const temporaryManifest = join(temporaryDirectory, "manifest.json");
    const lineEndingFile =
      "plugins/visio-scientific-illustrator/bridge/VisioBridge.sln";

    try {
      const manifest = await captureStableHashes({
        root,
        sourceCommit: SOURCE_COMMIT,
        manifestPath: temporaryManifest,
      });
      const sourceHash = createHash("sha256")
        .update(
          execFileSync("git", ["show", `${SOURCE_COMMIT}:${lineEndingFile}`], {
            cwd: root,
          }),
        )
        .digest("hex");

      assert.equal(manifest.files[lineEndingFile], sourceHash);
    } finally {
      rmSync(temporaryDirectory, { recursive: true, force: true });
    }
  });
}

if (requiredFilesExist) {
  const manifestText = readFileSync(manifestUrl, "utf8");
  const manifest = JSON.parse(manifestText);
  const trackedFiles = execFileSync(
    "git",
    ["ls-files", "-z", "--", ...protectedRoots],
    { cwd: root },
  )
    .toString("utf8")
    .split("\0")
    .filter(Boolean)
    .map((file) => file.replaceAll("\\", "/"))
    .sort((left, right) => left.localeCompare(right, "en"));

  test("基线清单固定来源并覆盖全部受保护的 Git 已跟踪文件", () => {
    assert.equal(manifest.algorithm, "sha256");
    assert.equal(manifest.source_commit, SOURCE_COMMIT);
    assert.deepEqual(manifest.protected_roots, protectedRoots);
    assert.ok(Object.keys(manifest.files).length > 100);
    assert.deepEqual(Object.keys(manifest.files), trackedFiles);
    assert.ok(Object.keys(manifest.files).every((file) => !file.includes("\\")));
    assert.ok(
      Object.values(manifest.files).every((hash) => /^[a-f0-9]{64}$/u.test(hash)),
    );
    assert.equal(manifestText, `${JSON.stringify(manifest, null, 2)}\n`);
  });

  test("稳定版文件未变化时校验通过", async () => {
    const { verifyStableHashes } = await import(verifyScriptUrl);

    assert.deepEqual(await verifyStableHashes({ root, manifest }), {
      ok: true,
      changed: [],
      missing: [],
      unexpected: [],
    });
  });

  test("校验同时区分内容变化、缺失和意外文件", async () => {
    const { verifyStableHashes } = await import(verifyScriptUrl);
    const [firstFile, secondFile, ...remainingFiles] = Object.keys(manifest.files);
    const changedManifest = structuredClone(manifest);
    const missingManifest = structuredClone(manifest);
    const unexpectedManifest = structuredClone(manifest);

    changedManifest.files[firstFile] = "0".repeat(64);
    missingManifest.files["plugins/visio-scientific-illustrator/missing.test"] =
      "0".repeat(64);
    unexpectedManifest.files = Object.fromEntries(
      [firstFile, ...remainingFiles].map((file) => [file, manifest.files[file]]),
    );

    assert.deepEqual(await verifyStableHashes({ root, manifest: changedManifest }), {
      ok: false,
      changed: [firstFile],
      missing: [],
      unexpected: [],
    });
    assert.deepEqual(await verifyStableHashes({ root, manifest: missingManifest }), {
      ok: false,
      changed: [],
      missing: ["plugins/visio-scientific-illustrator/missing.test"],
      unexpected: [],
    });
    assert.deepEqual(
      await verifyStableHashes({ root, manifest: unexpectedManifest }),
      {
        ok: false,
        changed: [],
        missing: [],
        unexpected: [secondFile],
      },
    );
  });

  test("捕获脚本拒绝覆盖已经存在的基线", async () => {
    const { captureStableHashes } = await import(captureScriptUrl);

    await assert.rejects(
      captureStableHashes({
        root,
        sourceCommit: SOURCE_COMMIT,
        manifestPath: fileURLToPath(manifestUrl),
      }),
      /refusing to overwrite/u,
    );
  });
}
