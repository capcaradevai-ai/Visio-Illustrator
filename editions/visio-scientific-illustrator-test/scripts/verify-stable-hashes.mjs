import { existsSync, readFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import {
  ALGORITHM,
  EXPECTED_SOURCE_COMMIT,
  PROTECTED_ROOTS,
  hashProtectedSourceFiles,
  listChangedProtectedFiles,
  listProtectedFiles,
} from "./capture-stable-hashes.mjs";

const defaultRoot = fileURLToPath(new URL("../../../", import.meta.url));
const defaultManifestPath = fileURLToPath(
  new URL("../stable-protected.sha256.json", import.meta.url),
);
const comparePaths = (left, right) => left.localeCompare(right, "en");

function validateManifest(manifest) {
  if (manifest === null || typeof manifest !== "object") {
    throw new Error("Stable hash manifest must be a JSON object.");
  }
  if (manifest.algorithm !== ALGORITHM) {
    throw new Error(`Stable hash algorithm must be ${ALGORITHM}.`);
  }
  if (manifest.source_commit !== EXPECTED_SOURCE_COMMIT) {
    throw new Error(
      `Stable hash source commit must be ${EXPECTED_SOURCE_COMMIT}.`,
    );
  }
  if (
    !Array.isArray(manifest.protected_roots) ||
    manifest.protected_roots.length !== PROTECTED_ROOTS.length ||
    manifest.protected_roots.some(
      (root, index) => root !== PROTECTED_ROOTS[index],
    )
  ) {
    throw new Error("Stable hash protected roots do not match the fixed contract.");
  }
  if (
    manifest.files === null ||
    typeof manifest.files !== "object" ||
    Array.isArray(manifest.files)
  ) {
    throw new Error("Stable hash files must be an object.");
  }

  for (const [file, hash] of Object.entries(manifest.files)) {
    if (
      file.length === 0 ||
      file.includes("\\") ||
      isAbsolute(file) ||
      file === ".." ||
      file.startsWith("../") ||
      file.includes("/../")
    ) {
      throw new Error(`Invalid stable hash path: ${file}`);
    }
    if (!/^[a-f0-9]{64}$/u.test(hash)) {
      throw new Error(`Invalid ${ALGORITHM} hash for ${file}.`);
    }
  }
}

export async function verifyStableHashes({ root = defaultRoot, manifest } = {}) {
  validateManifest(manifest);

  const repositoryRoot = resolve(root);
  const trackedFiles = listProtectedFiles({ root: repositoryRoot });
  const trackedSet = new Set(trackedFiles);
  const sourceHashes = hashProtectedSourceFiles({
    root: repositoryRoot,
    sourceCommit: manifest.source_commit,
  });
  const sourceFiles = Object.keys(sourceHashes);
  const expectedFiles = Object.keys(manifest.files).sort(comparePaths);
  const expectedSet = new Set(expectedFiles);
  const changedSet = new Set();
  const missing = [];

  for (const file of expectedFiles) {
    const absolutePath = resolve(repositoryRoot, ...file.split("/"));
    if (
      !Object.hasOwn(sourceHashes, file) ||
      !trackedSet.has(file) ||
      !existsSync(absolutePath)
    ) {
      missing.push(file);
      continue;
    }
    if (sourceHashes[file] !== manifest.files[file]) {
      changedSet.add(file);
    }
  }

  for (const file of listChangedProtectedFiles({
    root: repositoryRoot,
    sourceCommit: manifest.source_commit,
  })) {
    const absolutePath = resolve(repositoryRoot, ...file.split("/"));
    if (
      expectedSet.has(file) &&
      trackedSet.has(file) &&
      Object.hasOwn(sourceHashes, file) &&
      existsSync(absolutePath)
    ) {
      changedSet.add(file);
    }
  }

  const unexpected = [...new Set([...sourceFiles, ...trackedFiles])]
    .filter((file) => !expectedSet.has(file))
    .sort(comparePaths);
  const changed = [...changedSet].sort(comparePaths);
  const result = {
    ok: changed.length === 0 && missing.length === 0 && unexpected.length === 0,
    changed,
    missing: missing.sort(comparePaths),
    unexpected,
  };
  return result;
}

function printFailures(result) {
  if (result.changed.length) {
    console.error(`Changed: ${result.changed.join(", ")}`);
  }
  if (result.missing.length) {
    console.error(`Missing: ${result.missing.join(", ")}`);
  }
  if (result.unexpected.length) {
    console.error(`Unexpected: ${result.unexpected.join(", ")}`);
  }
}

function isMainModule() {
  return (
    process.argv[1] !== undefined &&
    pathToFileURL(resolve(process.argv[1])).href === import.meta.url
  );
}

if (isMainModule()) {
  try {
    const manifest = JSON.parse(readFileSync(defaultManifestPath, "utf8"));
    const result = await verifyStableHashes({ manifest });
    if (result.ok) {
      console.log("Stable protected files unchanged.");
    } else {
      printFailures(result);
      process.exitCode = 1;
    }
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
