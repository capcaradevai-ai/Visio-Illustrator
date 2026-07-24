import assert from "node:assert/strict";
import { promises as fs } from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

test("Claude installer validates prerequisites and installs only Claude plugin state", async () => {
  const script = await fs.readFile(path.join(root, "install-claude.ps1"), "utf8");
  assert.match(script, /Get-Command claude/);
  assert.match(script, /Get-Command node/);
  assert.match(script, /Visio\.Application/);
  assert.match(script, /claude plugin validate/);
  assert.match(script, /plugin marketplace add/);
  assert.match(script, /plugin install/);
  assert.match(script, /plugin list --json/);
  assert.match(script, /plugin details/);
  assert.match(script, /--scope user/);
  assert.match(
    script,
    /visio-scientific-illustrator@visio-scientific-tools/,
  );
  assert.doesNotMatch(script, /codex plugin/);
});
