import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  writeFileSync,
} from "node:fs";
import { dirname, isAbsolute, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export const ALGORITHM = "sha256";
export const EXPECTED_SOURCE_COMMIT =
  "69df39c641f38574110563ee343f2a4d8fad30a3";
export const PROTECTED_ROOTS = [
  "plugins/visio-scientific-illustrator",
  ".agents/plugins/marketplace.json",
  ".claude-plugin/marketplace.json",
  "install.ps1",
  "install-claude.ps1",
];

const defaultRoot = fileURLToPath(new URL("../../../", import.meta.url));
const defaultManifestPath = fileURLToPath(
  new URL("../stable-protected.sha256.json", import.meta.url),
);
const comparePaths = (left, right) => left.localeCompare(right, "en");

function runGit(root, args, input) {
  return execFileSync("git", args, {
    cwd: root,
    encoding: "buffer",
    input,
    maxBuffer: 64 * 1024 * 1024,
    stdio: [input === undefined ? "ignore" : "pipe", "pipe", "pipe"],
  });
}

function normalizeTrackedPath(file) {
  const normalized = file.replaceAll("\\", "/");
  if (
    normalized.length === 0 ||
    isAbsolute(normalized) ||
    normalized === ".." ||
    normalized.startsWith("../") ||
    normalized.includes("/../")
  ) {
    throw new Error(`Unsafe tracked path: ${file}`);
  }
  return normalized;
}

function parseNullSeparatedPaths(output) {
  return output
    .toString("utf8")
    .split("\0")
    .filter(Boolean)
    .map(normalizeTrackedPath)
    .sort(comparePaths);
}

export function listProtectedFiles({ root, sourceCommit } = {}) {
  const repositoryRoot = resolve(root ?? defaultRoot);
  if (sourceCommit) {
    return listProtectedSourceEntries({
      root: repositoryRoot,
      sourceCommit,
    }).map(({ file }) => file);
  }
  return parseNullSeparatedPaths(
    runGit(repositoryRoot, ["ls-files", "-z", "--", ...PROTECTED_ROOTS]),
  );
}

function hashBytes(bytes) {
  return createHash(ALGORITHM).update(bytes).digest("hex");
}

function listProtectedSourceEntries({ root, sourceCommit }) {
  const output = runGit(root, [
    "ls-tree",
    "-r",
    "-z",
    sourceCommit,
    "--",
    ...PROTECTED_ROOTS,
  ]);
  return output
    .toString("utf8")
    .split("\0")
    .filter(Boolean)
    .map((record) => {
      const separator = record.indexOf("\t");
      if (separator < 0) {
        throw new Error(`Invalid git ls-tree record: ${record}`);
      }
      const [mode, type, objectName] = record
        .slice(0, separator)
        .split(" ");
      if (!mode || type !== "blob" || !/^[a-f0-9]+$/u.test(objectName)) {
        throw new Error(`Unsupported protected git object: ${record}`);
      }
      return {
        file: normalizeTrackedPath(record.slice(separator + 1)),
        objectName,
      };
    })
    .sort((left, right) => comparePaths(left.file, right.file));
}

function parseBatchObjects(output, entries) {
  const hashes = {};
  let offset = 0;

  for (const entry of entries) {
    const headerEnd = output.indexOf(0x0a, offset);
    if (headerEnd < 0) {
      throw new Error(`Missing git cat-file header for ${entry.file}.`);
    }
    const [objectName, type, sizeText] = output
      .subarray(offset, headerEnd)
      .toString("utf8")
      .split(" ");
    const size = Number.parseInt(sizeText, 10);
    if (
      objectName !== entry.objectName ||
      type !== "blob" ||
      !Number.isSafeInteger(size) ||
      size < 0
    ) {
      throw new Error(`Invalid git cat-file header for ${entry.file}.`);
    }

    const contentStart = headerEnd + 1;
    const contentEnd = contentStart + size;
    if (contentEnd >= output.length || output[contentEnd] !== 0x0a) {
      throw new Error(`Invalid git cat-file body for ${entry.file}.`);
    }
    hashes[entry.file] = hashBytes(output.subarray(contentStart, contentEnd));
    offset = contentEnd + 1;
  }

  if (offset !== output.length) {
    throw new Error("Unexpected trailing data from git cat-file.");
  }
  return hashes;
}

export function hashProtectedSourceFiles({ root, sourceCommit }) {
  const repositoryRoot = resolve(root ?? defaultRoot);
  const entries = listProtectedSourceEntries({
    root: repositoryRoot,
    sourceCommit,
  });
  const input = Buffer.from(
    `${entries.map(({ objectName }) => objectName).join("\n")}\n`,
    "utf8",
  );
  const output = runGit(
    repositoryRoot,
    ["cat-file", "--batch"],
    input,
  );
  return parseBatchObjects(output, entries);
}

export function listChangedProtectedFiles({ root, sourceCommit }) {
  const repositoryRoot = resolve(root ?? defaultRoot);
  return parseNullSeparatedPaths(
    runGit(repositoryRoot, [
      "diff",
      "--name-only",
      "-z",
      sourceCommit,
      "--",
      ...PROTECTED_ROOTS,
    ]),
  );
}

function compareFileSets(expected, actual) {
  const expectedSet = new Set(expected);
  const actualSet = new Set(actual);
  return {
    missing: expected.filter((file) => !actualSet.has(file)).sort(comparePaths),
    unexpected: actual
      .filter((file) => !expectedSet.has(file))
      .sort(comparePaths),
  };
}

function assertWorkspaceMatchesSource({
  root,
  sourceCommit,
  sourceFiles,
  workspaceFiles,
}) {
  const { missing, unexpected } = compareFileSets(sourceFiles, workspaceFiles);
  if (missing.length || unexpected.length) {
    throw new Error(
      [
        "Stable protected file set does not match the requested source commit.",
        missing.length ? `Missing: ${missing.join(", ")}` : "",
        unexpected.length ? `Unexpected: ${unexpected.join(", ")}` : "",
      ]
        .filter(Boolean)
        .join("\n"),
    );
  }

  // Git 的文本规范化负责区分真实修改与 Windows 工作区换行差异。
  const changed = listChangedProtectedFiles({ root, sourceCommit });
  if (changed.length) {
    throw new Error(
      [
        "Stable protected files contain changes relative to the requested source commit.",
        `Changed: ${changed.join(", ")}`,
      ].join("\n"),
    );
  }
}

export async function captureStableHashes({
  root = defaultRoot,
  sourceCommit,
  manifestPath = defaultManifestPath,
} = {}) {
  const repositoryRoot = resolve(root);
  const outputPath = resolve(manifestPath);

  if (existsSync(outputPath)) {
    throw new Error(
      `Stable hash manifest already exists; refusing to overwrite: ${outputPath}`,
    );
  }
  if (!sourceCommit) {
    throw new Error("--source-commit is required.");
  }

  const resolvedSourceCommit = runGit(repositoryRoot, [
    "rev-parse",
    "--verify",
    `${sourceCommit}^{commit}`,
  ])
    .toString("utf8")
    .trim();
  if (resolvedSourceCommit !== EXPECTED_SOURCE_COMMIT) {
    throw new Error(
      `Source commit must be ${EXPECTED_SOURCE_COMMIT}; received ${resolvedSourceCommit}.`,
    );
  }

  const sourceFiles = listProtectedFiles({
    root: repositoryRoot,
    sourceCommit: resolvedSourceCommit,
  });
  const workspaceFiles = listProtectedFiles({ root: repositoryRoot });

  assertWorkspaceMatchesSource({
    root: repositoryRoot,
    sourceCommit: resolvedSourceCommit,
    sourceFiles,
    workspaceFiles,
  });
  const sourceHashes = hashProtectedSourceFiles({
    root: repositoryRoot,
    sourceCommit: resolvedSourceCommit,
  });

  const manifest = {
    algorithm: ALGORITHM,
    source_commit: resolvedSourceCommit,
    protected_roots: PROTECTED_ROOTS,
    files: sourceHashes,
  };
  mkdirSync(dirname(outputPath), { recursive: true });
  writeFileSync(outputPath, `${JSON.stringify(manifest, null, 2)}\n`, {
    encoding: "utf8",
    flag: "wx",
  });
  return manifest;
}

function readSourceCommitArgument(args) {
  if (args.length !== 2 || args[0] !== "--source-commit" || !args[1]) {
    throw new Error(
      "Usage: node capture-stable-hashes.mjs --source-commit <full-commit>",
    );
  }
  return args[1];
}

function isMainModule() {
  return (
    process.argv[1] !== undefined &&
    pathToFileURL(resolve(process.argv[1])).href === import.meta.url
  );
}

if (isMainModule()) {
  try {
    const sourceCommit = readSourceCommitArgument(process.argv.slice(2));
    await captureStableHashes({ sourceCommit });
    console.log(`Captured stable protected hashes: ${defaultManifestPath}`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
