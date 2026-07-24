import { promises as fs } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const marketplacePath = path.join(root, ".agents", "plugins", "marketplace.json");
const marketplace = JSON.parse(await fs.readFile(marketplacePath, "utf8"));

if (marketplace.name !== "visio-scientific-tools") throw new Error("Unexpected marketplace name.");
if (!Array.isArray(marketplace.plugins) || marketplace.plugins.length !== 1) throw new Error("Marketplace must expose exactly one plugin.");

const entry = marketplace.plugins[0];
const pluginRoot = path.resolve(path.dirname(marketplacePath), "..", "..", entry.source.path);
const manifest = JSON.parse(await fs.readFile(path.join(pluginRoot, ".codex-plugin", "plugin.json"), "utf8"));
const packageJson = JSON.parse(await fs.readFile(path.join(root, "package.json"), "utf8"));
const mcp = JSON.parse(await fs.readFile(path.join(pluginRoot, ".mcp.json"), "utf8"));
const claudeMarketplacePath = path.join(root, ".claude-plugin", "marketplace.json");
const claudeMarketplace = JSON.parse(
  await fs.readFile(claudeMarketplacePath, "utf8"),
);
const claudeManifestPath = path.join(
  pluginRoot,
  ".claude-plugin",
  "plugin.json",
);
const claudeManifest = JSON.parse(await fs.readFile(claudeManifestPath, "utf8"));
const claudeMcpPath = path.join(pluginRoot, ".mcp.claude.json");
const claudeMcp = JSON.parse(await fs.readFile(claudeMcpPath, "utf8"));

if (entry.name !== "visio-scientific-illustrator") throw new Error("Unexpected plugin name.");
if (entry.name !== manifest.name) throw new Error("Marketplace and manifest plugin names differ.");
if (!/^\d+\.\d+\.\d+$/.test(manifest.version)) throw new Error("Plugin version must use semantic versioning.");
if (manifest.version !== packageJson.version) throw new Error("Plugin and package versions differ.");
if (!mcp.mcpServers?.["visio-live"] || !mcp.mcpServers?.["visio-file-utils"]) {
  throw new Error("Required Visio MCP servers are missing.");
}
if (claudeMarketplace.name !== marketplace.name) {
  throw new Error("Codex and Claude marketplace names differ.");
}
if (claudeMarketplace.plugins?.length !== 1) {
  throw new Error("Claude marketplace must expose exactly one plugin.");
}
if (claudeMarketplace.plugins[0].name !== entry.name) {
  throw new Error("Codex and Claude plugin names differ.");
}
if (claudeMarketplace.plugins[0].source !== entry.source.path) {
  throw new Error("Codex and Claude plugin roots differ.");
}
if (claudeManifest.name !== manifest.name) {
  throw new Error("Codex and Claude manifest names differ.");
}
if (claudeManifest.version !== manifest.version) {
  throw new Error("Codex and Claude manifest versions differ.");
}
if (claudeManifest.mcpServers !== "./.mcp.claude.json") {
  throw new Error("Claude manifest must reference .mcp.claude.json.");
}
for (const name of ["visio-live", "visio-file-utils"]) {
  const definition = claudeMcp.mcpServers?.[name];
  if (!definition || definition.command !== "node") {
    throw new Error(`Claude MCP server is missing: ${name}`);
  }
  if (definition.cwd !== undefined) {
    throw new Error(`Claude MCP server must not declare cwd: ${name}`);
  }
  if (!definition.args?.[0]?.startsWith("${CLAUDE_PLUGIN_ROOT}/scripts/")) {
    throw new Error(`Claude MCP server path is not cache-safe: ${name}`);
  }
}

const files = [
  marketplacePath,
  claudeMarketplacePath,
  path.join(pluginRoot, ".codex-plugin", "plugin.json"),
  claudeManifestPath,
  path.join(pluginRoot, ".mcp.json"),
  claudeMcpPath,
  path.join(pluginRoot, "scripts", "live-server.mjs"),
  path.join(pluginRoot, "scripts", "file-server.mjs"),
];

for (const file of files) {
  const text = await fs.readFile(file, "utf8");
  if (/C:\\Users\\[^\\]+|C:\/Users\/[^/]+|ProgramData\\miniconda3|gho_|github_pat_/i.test(text)) {
    throw new Error(`Local path or credential-like value found in ${path.relative(root, file)}.`);
  }
}

console.log("Repository structure and portability checks passed.");
