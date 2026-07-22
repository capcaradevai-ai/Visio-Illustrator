#!/usr/bin/env node

import { createInterface } from "node:readline";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { BridgeClient, resolveBridgeExecutable } from "./bridge-client.mjs";
import {
  rpcError,
  rpcResult,
  SerialQueue,
  toolResult,
} from "./mcp-common.mjs";
import { fileTools } from "./tool-schemas.mjs";

const serverName = "visio-file-utils";
const serverVersion = "0.1.0";
const supportedProtocols = new Set(["2024-11-05", "2025-03-26", "2025-06-18"]);
const queue = new SerialQueue();
const pluginRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
let bridgeClient = null;

async function getBridgeClient() {
  if (bridgeClient) return bridgeClient;
  const executable = resolveBridgeExecutable(pluginRoot);
  const client = new BridgeClient(executable, ["--mode", "file"], { cwd: pluginRoot });
  await client.start();
  bridgeClient = client;
  return bridgeClient;
}

async function handleTool(name, args) {
  const client = await getBridgeClient();
  if (name === "visio_validate") return client.call("validate", args);
  if (name === "visio_inspect_file") return client.call("inspect_file", args);
  if (name === "visio_export") return client.call("export", args, 120000);
  return {
    error: "UNSUPPORTED_OPERATION",
    message: `不支持工具: ${name}`,
    tool: name,
  };
}

async function handleMessage(message) {
  const { id, method, params } = message;
  if (method === "initialize") {
    const requested = params?.protocolVersion;
    return rpcResult(id, {
      protocolVersion: supportedProtocols.has(requested) ? requested : "2025-06-18",
      capabilities: { tools: { listChanged: false } },
      serverInfo: { name: serverName, version: serverVersion },
      instructions: "Use absolute paths for editable VSDX validation, inspection, and export.",
    });
  }
  if (method === "ping") return rpcResult(id, {});
  if (method === "tools/list") return rpcResult(id, { tools: fileTools });
  if (method === "tools/call") {
    try {
      const value = await handleTool(params?.name, params?.arguments || {});
      return rpcResult(id, toolResult(value, { isError: Boolean(value?.error) }));
    } catch (error) {
      return rpcResult(id, toolResult({
        error: error.bridgeError?.code || "BRIDGE_ERROR",
        message: error.message,
        retryable: error.bridgeError?.retryable ?? false,
        details: error.bridgeError?.details,
        tool: params?.name,
      }, { isError: true }));
    }
  }
  if (method?.startsWith("notifications/")) return null;
  return rpcError(id, -32601, `Method not found: ${method}`);
}

const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
lines.on("line", (line) => {
  if (!line.trim()) return;
  queue.run(async () => {
    let message;
    try {
      message = JSON.parse(line);
    } catch (error) {
      process.stdout.write(`${JSON.stringify(rpcError(null, -32700, "Parse error", error.message))}\n`);
      return;
    }
    try {
      const response = await handleMessage(message);
      if (response) process.stdout.write(`${JSON.stringify(response)}\n`);
    } catch (error) {
      process.stdout.write(`${JSON.stringify(rpcError(message.id, -32603, "Internal error", error.message))}\n`);
    }
  }).catch((error) => process.stderr.write(`[${serverName}] ${error.stack || error.message}\n`));
});
lines.on("close", () => {
  bridgeClient?.stop().catch((error) => {
    process.stderr.write(`[${serverName}] ${error.stack || error.message}\n`);
  });
});
