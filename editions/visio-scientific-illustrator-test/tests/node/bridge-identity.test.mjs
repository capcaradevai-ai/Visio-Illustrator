import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const editionRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../..",
);
const bridgeRoot = path.join(
  editionRoot,
  "plugins",
  "visio-scientific-illustrator-test",
  "bridge",
);

async function collectFiles(root, predicate) {
  const values = [];
  const visit = async (current) => {
    for (const entry of await fs.readdir(current, { withFileTypes: true })) {
      if (["bin", "obj", "TestResults"].includes(entry.name)) continue;
      const absolute = path.join(current, entry.name);
      if (entry.isDirectory()) await visit(absolute);
      else if (predicate(absolute)) values.push(absolute);
    }
  };
  await visit(root);
  return values;
}

test("Test Bridge solution and projects use isolated names", async () => {
  await fs.access(path.join(bridgeRoot, "VisioTestBridge.sln"));
  const projectFiles = await collectFiles(
    bridgeRoot,
    (file) => file.endsWith(".csproj"),
  );
  assert.equal(projectFiles.length, 5);
  for (const file of projectFiles) {
    assert.match(path.basename(file), /^VisioTestBridge\..+\.csproj$/);
    const text = await fs.readFile(file, "utf8");
    assert.doesNotMatch(text, /<RootNamespace>VisioBridge/);
    assert.doesNotMatch(text, /<AssemblyName>VisioBridge/);
    assert.doesNotMatch(text, /VisioBridge\.(?:Contracts|Core|Host|Tests)\.csproj/);
  }
});

test("Test Bridge C# namespaces are isolated", async () => {
  const sourceFiles = await collectFiles(
    bridgeRoot,
    (file) => file.endsWith(".cs"),
  );
  assert.ok(sourceFiles.length > 80);
  for (const file of sourceFiles) {
    const text = await fs.readFile(file, "utf8");
    assert.doesNotMatch(text, /\bnamespace VisioBridge(?:\.|;)/);
    assert.doesNotMatch(text, /\busing VisioBridge\./);
  }
});

test("Test Bridge build script publishes only VisioTestBridge.exe", async () => {
  const buildScript = await fs.readFile(
    path.join(editionRoot, "scripts", "build-bridge.ps1"),
    "utf8",
  );
  assert.match(buildScript, /VisioTestBridge\.Host\\VisioTestBridge\.Host\.csproj/);
  assert.match(buildScript, /VisioTestBridge\.exe/);
  assert.doesNotMatch(
    buildScript,
    /\$bridge\s*=\s*Join-Path \$output "VisioBridge\.exe"/,
  );
  assert.match(
    buildScript,
    /Test-Path -LiteralPath \(Join-Path \$output "VisioBridge\.exe"\)/,
  );
  assert.match(buildScript, /\\d\+\\\.\\d\+\\\.\\d\+-test\\\.\\d\+/);
});

test("Bridge ready contract identifies Test edition and protocol", async () => {
  const protocol = await fs.readFile(
    path.join(
      bridgeRoot,
      "VisioTestBridge.Contracts",
      "Protocol.cs",
    ),
    "utf8",
  );
  assert.match(protocol, /string Edition/);
  assert.match(protocol, /string ProtocolVersion/);
});
