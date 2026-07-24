import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const pluginRoot = path.join(root, "plugins", "visio-scientific-illustrator");
const expectedVersion = "0.3.0";

async function readJson(relativePath) {
  return JSON.parse(await fs.readFile(path.join(root, relativePath), "utf8"));
}

test("Codex and Claude manifests publish one plugin at the same version", async () => {
  const packageJson = await readJson("package.json");
  const codexMarketplace = await readJson(".agents/plugins/marketplace.json");
  const claudeMarketplace = await readJson(".claude-plugin/marketplace.json");
  const codexManifest = await readJson(
    "plugins/visio-scientific-illustrator/.codex-plugin/plugin.json",
  );
  const claudeManifest = await readJson(
    "plugins/visio-scientific-illustrator/.claude-plugin/plugin.json",
  );

  assert.equal(packageJson.version, expectedVersion);
  assert.equal(codexMarketplace.name, "visio-scientific-tools");
  assert.equal(claudeMarketplace.name, "visio-scientific-tools");
  assert.equal(codexMarketplace.plugins[0].name, "visio-scientific-illustrator");
  assert.equal(claudeMarketplace.plugins[0].name, "visio-scientific-illustrator");
  assert.equal(codexManifest.name, claudeManifest.name);
  assert.equal(codexManifest.version, expectedVersion);
  assert.equal(claudeManifest.version, expectedVersion);
});

test("Claude MCP configuration resolves scripts through CLAUDE_PLUGIN_ROOT", async () => {
  const mcp = await readJson(
    "plugins/visio-scientific-illustrator/.mcp.claude.json",
  );
  assert.deepEqual(Object.keys(mcp.mcpServers).sort(), [
    "visio-file-utils",
    "visio-live",
  ]);
  for (const definition of Object.values(mcp.mcpServers)) {
    assert.equal(definition.command, "node");
    assert.equal(definition.args.length, 1);
    assert.match(definition.args[0], /^\$\{CLAUDE_PLUGIN_ROOT\}\/scripts\/.+\.mjs$/);
    assert.equal("cwd" in definition, false);
  }
});

test("both platform manifests resolve to the same shared plugin root", async () => {
  const codexMarketplace = await readJson(".agents/plugins/marketplace.json");
  const claudeMarketplace = await readJson(".claude-plugin/marketplace.json");
  const codexSource = codexMarketplace.plugins[0].source.path;
  const claudeSource = claudeMarketplace.plugins[0].source;
  assert.equal(codexSource, "./plugins/visio-scientific-illustrator");
  assert.equal(claudeSource, "./plugins/visio-scientific-illustrator");
  await fs.access(path.join(pluginRoot, "scripts", "live-server.mjs"));
  await fs.access(path.join(pluginRoot, "scripts", "file-server.mjs"));
});
