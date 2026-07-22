#!/usr/bin/env node

import { createInterface } from "node:readline";
import {
  rpcError,
  rpcResult,
  SerialQueue,
  toolResult,
} from "./mcp-common.mjs";
import { liveTools } from "./tool-schemas.mjs";

const serverName = "visio-live";
const serverVersion = "0.1.0";
const supportedProtocols = new Set(["2024-11-05", "2025-03-26", "2025-06-18"]);
const queue = new SerialQueue();

async function handleMessage(message) {
  const { id, method, params } = message;
  if (method === "initialize") {
    const requested = params?.protocolVersion;
    return rpcResult(id, {
      protocolVersion: supportedProtocols.has(requested) ? requested : "2025-06-18",
      capabilities: { tools: { listChanged: false } },
      serverInfo: { name: serverName, version: serverVersion },
      instructions: "Use the Visio live tools to create editable geometry in a plugin-owned visible Visio window.",
    });
  }
  if (method === "ping") return rpcResult(id, {});
  if (method === "tools/list") return rpcResult(id, { tools: liveTools });
  if (method === "tools/call") {
    return rpcResult(id, toolResult({
      error: "BRIDGE_NOT_BUILT",
      message: "VisioBridge 尚未构建。",
      tool: params?.name,
    }, { isError: true }));
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
