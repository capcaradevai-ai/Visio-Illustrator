import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

test("Codex installer upgrades without deleting a cache used by a running app", async () => {
  const script = await fs.readFile(path.join(root, "install.ps1"), "utf8");
  assert.match(script, /plugin add \$pluginId/);
  assert.match(script, /\$existingMarketplace\.root/);
  assert.match(
    script,
    /if \(\[string\]\$installed\.version -ne \[string\]\$manifestVersion\) \{\s*& \$codex plugin add \$pluginId/s,
  );
  assert.doesNotMatch(script, /plugin remove/);
  assert.doesNotMatch(script, /plugin marketplace remove/);
});
