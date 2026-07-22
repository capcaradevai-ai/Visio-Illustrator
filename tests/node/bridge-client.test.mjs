import test from "node:test";
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  BridgeClient,
  resolveBridgeExecutable,
} from "../../plugins/visio-scientific-illustrator/scripts/bridge-client.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));

test("BridgeClient waits for ready and correlates responses", async () => {
  const client = new BridgeClient(process.execPath, [path.join(here, "fake-bridge.mjs")]);
  await client.start();

  const result = await client.call("status", {}, 1000);

  assert.equal(result.command, "status");
  await client.stop();
});

test("BridgeClient rejects timed out calls", async () => {
  const client = new BridgeClient(process.execPath, [path.join(here, "fake-bridge.mjs")]);
  await client.start();

  await assert.rejects(() => client.call("hang", {}, 20), /BRIDGE_TIMEOUT/);

  await client.stop();
});

test("BridgeClient stop does not keep the Node process alive", async () => {
  const child = spawn(process.execPath, [path.join(here, "bridge-stop-runner.mjs")], {
    stdio: "ignore",
  });
  const started = Date.now();

  const [code] = await once(child, "exit");

  assert.equal(code, 0);
  assert.ok(Date.now() - started < 1000);
});

test("resolveBridgeExecutable rejects a missing bundled bridge", () => {
  const original = process.env.VISIO_BRIDGE_PATH;
  delete process.env.VISIO_BRIDGE_PATH;
  try {
    assert.throws(
      () => resolveBridgeExecutable(path.join(here, "missing-plugin")),
      /BRIDGE_NOT_FOUND/,
    );
  } finally {
    if (original === undefined) delete process.env.VISIO_BRIDGE_PATH;
    else process.env.VISIO_BRIDGE_PATH = original;
  }
});
