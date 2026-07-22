import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  fileTools,
  liveTools,
} from "../plugins/visio-scientific-illustrator/scripts/tool-schemas.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const cases = [
  {
    file: "plugins/visio-scientific-illustrator/scripts/live-server.mjs",
    expectedNames: liveTools.map((tool) => tool.name),
  },
  {
    file: "plugins/visio-scientific-illustrator/scripts/file-server.mjs",
    expectedNames: fileTools.map((tool) => tool.name),
  },
];

async function testServer({ file, expectedNames }) {
  const child = spawn(process.execPath, [path.join(root, file)], {
    cwd: root,
    stdio: ["pipe", "pipe", "pipe"],
  });
  const lines = createInterface({ input: child.stdout, crlfDelay: Infinity });
  const responses = new Map();
  let stderr = "";
  child.stderr.on("data", (chunk) => { stderr += String(chunk); });

  const received = new Promise((resolve, reject) => {
    const timer = setTimeout(
      () => reject(new Error(`Timed out waiting for ${file}.\n${stderr}`)),
      8_000,
    );
    lines.on("line", (line) => {
      try {
        const message = JSON.parse(line);
        if (message.id !== undefined) responses.set(message.id, message);
        if ([1, 2, 3].every((id) => responses.has(id))) {
          clearTimeout(timer);
          resolve();
        }
      } catch (error) {
        clearTimeout(timer);
        reject(new Error(`Invalid MCP JSON from ${file}: ${error.message}`));
      }
    });
    child.once("error", reject);
    child.once("exit", (code) => {
      if (![1, 2, 3].every((id) => responses.has(id))) {
        clearTimeout(timer);
        reject(new Error(`${file} exited early with ${code}. ${stderr}`));
      }
    });
  });

  const send = (message) => child.stdin.write(`${JSON.stringify(message)}\n`);
  send({
    jsonrpc: "2.0",
    id: 1,
    method: "initialize",
    params: {
      protocolVersion: "2025-06-18",
      capabilities: {},
      clientInfo: { name: "smoke-test", version: "1.0.0" },
    },
  });
  send({ jsonrpc: "2.0", id: 2, method: "tools/list", params: {} });
  send({
    jsonrpc: "2.0",
    id: 3,
    method: "tools/call",
    params: { name: "visio_unknown_tool", arguments: {} },
  });

  try {
    await received;
    assert.equal(responses.get(1).error, undefined, `${file}: initialize failed`);
    assert.deepEqual(
      responses.get(2).result.tools.map((tool) => tool.name),
      expectedNames,
      `${file}: unexpected tool names`,
    );
    assert.equal(
      responses.get(3).result.isError,
      true,
      `${file}: unknown tool was not reported as an MCP tool error`,
    );

    child.stdin.end();
    const exitCode = await Promise.race([
      new Promise((resolve) => child.once("exit", resolve)),
      new Promise((_, reject) => setTimeout(
        () => reject(new Error(`${file} did not exit after stdin closed.`)),
        5_000,
      )),
    ]);
    assert.equal(exitCode, 0, `${file}: nonzero exit. ${stderr}`);
    console.log(`${file}: ${expectedNames.length} tools`);
  } finally {
    lines.close();
    if (child.exitCode === null) child.kill();
  }
}

for (const entry of cases) await testServer(entry);
console.log("MCP smoke tests passed.");
