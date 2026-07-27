import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const editionRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../..",
);
const repositoryRoot = path.resolve(editionRoot, "../..");
const pluginRoot = path.join(
  editionRoot,
  "plugins",
  "visio-scientific-illustrator-test",
);

const expected = {
  packageName: "visio-scientific-tools-test",
  pluginName: "visio-scientific-illustrator-test",
  displayName: "Visio Scientific Illustrator Test",
  version: "0.4.0-test.1",
  liveServer: "visio-test-live",
  fileServer: "visio-test-file-utils",
};

async function readJson(filePath) {
  return JSON.parse(await fs.readFile(filePath, "utf8"));
}

async function listRuntimeTextFiles(root) {
  const files = [];
  const visit = async (current) => {
    for (const entry of await fs.readdir(current, { withFileTypes: true })) {
      if (["bin", "obj", "TestResults"].includes(entry.name)) continue;
      const absolute = path.join(current, entry.name);
      if (entry.isDirectory()) {
        await visit(absolute);
      } else if (/\.(?:json|mjs|md|cs|csproj|sln|ps1|ya?ml)$/i.test(entry.name)) {
        files.push(absolute);
      }
    }
  };
  await visit(root);
  return files;
}

test("Test Edition manifests expose only the isolated identity", async () => {
  const packageJson = await readJson(path.join(editionRoot, "package.json"));
  const codexMarketplace = await readJson(
    path.join(editionRoot, ".agents", "plugins", "marketplace.json"),
  );
  const claudeMarketplace = await readJson(
    path.join(editionRoot, ".claude-plugin", "marketplace.json"),
  );
  const codexManifest = await readJson(
    path.join(pluginRoot, ".codex-plugin", "plugin.json"),
  );
  const claudeManifest = await readJson(
    path.join(pluginRoot, ".claude-plugin", "plugin.json"),
  );

  assert.equal(packageJson.name, expected.packageName);
  assert.equal(packageJson.version, expected.version);
  assert.equal(codexMarketplace.name, expected.packageName);
  assert.equal(claudeMarketplace.name, expected.packageName);
  assert.equal(codexMarketplace.plugins.length, 1);
  assert.equal(claudeMarketplace.plugins.length, 1);
  assert.equal(codexMarketplace.plugins[0].name, expected.pluginName);
  assert.equal(claudeMarketplace.plugins[0].name, expected.pluginName);
  assert.equal(codexManifest.name, expected.pluginName);
  assert.equal(claudeManifest.name, expected.pluginName);
  assert.equal(codexManifest.version, expected.version);
  assert.equal(claudeManifest.version, expected.version);
  assert.equal(codexManifest.interface.displayName, expected.displayName);
});

test("Test Edition MCP server names do not collide with Stable", async () => {
  const codexMcp = await readJson(path.join(pluginRoot, ".mcp.json"));
  const claudeMcp = await readJson(path.join(pluginRoot, ".mcp.claude.json"));
  const expectedServers = [expected.fileServer, expected.liveServer].sort();

  assert.deepEqual(Object.keys(codexMcp.mcpServers).sort(), expectedServers);
  assert.deepEqual(Object.keys(claudeMcp.mcpServers).sort(), expectedServers);
  const stableLiveServer = ["visio", "live"].join("-");
  const stableFileServer = ["visio", "file", "utils"].join("-");
  assert.equal(codexMcp.mcpServers[stableLiveServer], undefined);
  assert.equal(claudeMcp.mcpServers[stableFileServer], undefined);
});

test("Test Edition runtime has no path dependency on the Stable plugin", async () => {
  const files = await listRuntimeTextFiles(pluginRoot);
  const forbiddenImport = /(?:\.\.\\|\.\.\/){2,}plugins[\\/]visio-scientific-illustrator(?:[\\/]|["'])/i;
  const stableAbsolute = path
    .join(repositoryRoot, "plugins", "visio-scientific-illustrator")
    .replaceAll("\\", "\\\\");

  for (const file of files) {
    const content = await fs.readFile(file, "utf8");
    assert.doesNotMatch(content, forbiddenImport, path.relative(editionRoot, file));
    assert.equal(
      content.includes(stableAbsolute),
      false,
      path.relative(editionRoot, file),
    );
  }
});

test("Test Edition repository validator accepts the isolated package", () => {
  const result = spawnSync(
    process.execPath,
    [path.join(editionRoot, "scripts", "validate-repo.mjs")],
    {
      cwd: editionRoot,
      encoding: "utf8",
    },
  );
  assert.equal(result.status, 0, result.stderr);
  assert.match(
    result.stdout,
    /Test Edition repository structure and portability checks passed\./,
  );
});
