import test from "node:test";
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { createInterface } from "node:readline";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");

test("live server rejects invalid arguments before starting the Bridge", async () => {
  const response = await callServer(
    "plugins/visio-scientific-illustrator-test/scripts/live-server.mjs",
    {
      jsonrpc: "2.0",
      id: 1,
      method: "tools/call",
      params: {
        name: "visio_test_live_fit",
        arguments: { mode: "selection", unknown: true },
      },
    },
  );

  assert.equal(response.result.isError, true);
  assert.equal(response.result.structuredContent.error, "INVALID_ARGUMENT");
  assert.match(response.result.structuredContent.details.field_path, /^\$\./);
});

test("file server reports an unknown tool as UNSUPPORTED_OPERATION", async () => {
  const response = await callServer(
    "plugins/visio-scientific-illustrator-test/scripts/file-server.mjs",
    {
      jsonrpc: "2.0",
      id: 2,
      method: "tools/call",
      params: { name: "visio_unknown", arguments: {} },
    },
  );

  assert.equal(response.result.isError, true);
  assert.equal(response.result.structuredContent.error, "UNSUPPORTED_OPERATION");
  assert.equal(response.result.structuredContent.tool, "visio_unknown");
});

async function callServer(relativePath, request) {
  const child = spawn(process.execPath, [path.join(root, relativePath)], {
    stdio: ["pipe", "pipe", "pipe"],
  });
  const lines = createInterface({ input: child.stdout, crlfDelay: Infinity });
  const responsePromise = new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("server response timeout")), 5000);
    lines.once("line", (line) => {
      clearTimeout(timer);
      try {
        resolve(JSON.parse(line));
      } catch (error) {
        reject(error);
      }
    });
  });
  child.stdin.end(`${JSON.stringify(request)}\n`);
  const response = await responsePromise;
  const [exitCode] = await once(child, "exit");
  lines.close();
  assert.equal(exitCode, 0);
  return response;
}
