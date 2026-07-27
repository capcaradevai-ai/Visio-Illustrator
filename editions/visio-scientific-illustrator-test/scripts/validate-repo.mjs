import { promises as fs } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { verifyStableHashes } from "./verify-stable-hashes.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const expected = {
  marketplace: "visio-scientific-tools-test",
  plugin: "visio-scientific-illustrator-test",
  version: "0.4.0-test.1",
  servers: ["visio-test-file-utils", "visio-test-live"],
};

async function readJson(filePath) {
  return JSON.parse(await fs.readFile(filePath, "utf8"));
}

const codexMarketplacePath = path.join(
  root,
  ".agents",
  "plugins",
  "marketplace.json",
);
const claudeMarketplacePath = path.join(
  root,
  ".claude-plugin",
  "marketplace.json",
);
const codexMarketplace = await readJson(codexMarketplacePath);
const claudeMarketplace = await readJson(claudeMarketplacePath);

if (codexMarketplace.name !== expected.marketplace) {
  throw new Error("Unexpected Test Codex marketplace name.");
}
if (claudeMarketplace.name !== expected.marketplace) {
  throw new Error("Unexpected Test Claude marketplace name.");
}
if (codexMarketplace.plugins?.length !== 1) {
  throw new Error("Test Codex marketplace must expose exactly one plugin.");
}
if (claudeMarketplace.plugins?.length !== 1) {
  throw new Error("Test Claude marketplace must expose exactly one plugin.");
}

const entry = codexMarketplace.plugins[0];
const pluginRoot = path.resolve(
  path.dirname(codexMarketplacePath),
  "..",
  "..",
  entry.source.path,
);
const packageJson = await readJson(path.join(root, "package.json"));
const codexManifestPath = path.join(
  pluginRoot,
  ".codex-plugin",
  "plugin.json",
);
const claudeManifestPath = path.join(
  pluginRoot,
  ".claude-plugin",
  "plugin.json",
);
const codexManifest = await readJson(codexManifestPath);
const claudeManifest = await readJson(claudeManifestPath);
const codexMcpPath = path.join(pluginRoot, ".mcp.json");
const claudeMcpPath = path.join(pluginRoot, ".mcp.claude.json");
const codexMcp = await readJson(codexMcpPath);
const claudeMcp = await readJson(claudeMcpPath);

if (packageJson.name !== expected.marketplace) {
  throw new Error("Unexpected Test package name.");
}
if (entry.name !== expected.plugin || codexManifest.name !== expected.plugin) {
  throw new Error("Test Codex plugin identity is inconsistent.");
}
if (
  claudeMarketplace.plugins[0].name !== expected.plugin ||
  claudeManifest.name !== expected.plugin
) {
  throw new Error("Test Claude plugin identity is inconsistent.");
}
if (
  packageJson.version !== expected.version ||
  codexManifest.version !== expected.version ||
  claudeManifest.version !== expected.version
) {
  throw new Error("Test package and plugin versions differ.");
}
if (!/^\d+\.\d+\.\d+-test\.\d+$/u.test(expected.version)) {
  throw new Error("Test version contract is invalid.");
}

for (const [label, mcp] of [
  ["Codex", codexMcp],
  ["Claude", claudeMcp],
]) {
  const names = Object.keys(mcp.mcpServers ?? {}).sort();
  if (JSON.stringify(names) !== JSON.stringify(expected.servers)) {
    throw new Error(`${label} Test MCP server identities are invalid.`);
  }
}
for (const name of expected.servers) {
  const definition = claudeMcp.mcpServers[name];
  if (
    definition.command !== "node" ||
    definition.cwd !== undefined ||
    !definition.args?.[0]?.startsWith("${CLAUDE_PLUGIN_ROOT}/scripts/")
  ) {
    throw new Error(`Claude Test MCP path is not cache-safe: ${name}`);
  }
}

const bridgePath = path.join(
  pluginRoot,
  "bin",
  "win-x64",
  "VisioTestBridge.exe",
);
await fs.access(bridgePath);

const portableFiles = [
  codexMarketplacePath,
  claudeMarketplacePath,
  codexManifestPath,
  claudeManifestPath,
  codexMcpPath,
  claudeMcpPath,
  path.join(pluginRoot, "scripts", "live-server.mjs"),
  path.join(pluginRoot, "scripts", "file-server.mjs"),
];
for (const file of portableFiles) {
  const text = await fs.readFile(file, "utf8");
  if (
    /C:\\Users\\[^\\]+|C:\/Users\/[^/]+|ProgramData\\miniconda3|gho_|github_pat_/iu.test(
      text,
    )
  ) {
    throw new Error(`Local path or credential-like value found in ${path.relative(root, file)}.`);
  }
  if (text.includes("\uFFFD")) {
    throw new Error(`Invalid UTF-8 replacement character found in ${path.relative(root, file)}.`);
  }
}

const stableManifest = await readJson(
  path.join(root, "stable-protected.sha256.json"),
);
const stable = await verifyStableHashes({ manifest: stableManifest });
if (!stable.ok) {
  throw new Error(`Stable protected files changed: ${JSON.stringify(stable)}`);
}

console.log("Test Edition repository structure and portability checks passed.");
console.log("Stable protected files unchanged.");
