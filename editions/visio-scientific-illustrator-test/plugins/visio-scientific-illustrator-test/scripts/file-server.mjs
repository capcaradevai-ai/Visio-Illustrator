#!/usr/bin/env node

import { createInterface } from "node:readline";
import { randomUUID } from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { BridgeClient, resolveBridgeExecutable } from "./bridge-client.mjs";
import {
  rpcError,
  rpcResult,
  mapToolError,
  SerialQueue,
  toolResult,
  validateSchema,
} from "./mcp-common.mjs";
import { fileTools, findTool } from "./tool-schemas.mjs";
import { comparePngFiles } from "./visual-compare.mjs";
import { BridgeReferenceImageBackend } from "./reference-image.mjs";
import { ReferenceStore } from "./reference-store.mjs";

const serverName = "visio-test-file-utils";
const pluginRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const serverVersion = JSON.parse(await fs.readFile(path.join(pluginRoot, ".codex-plugin", "plugin.json"), "utf8")).version;
const supportedProtocols = new Set(["2024-11-05", "2025-03-26", "2025-06-18"]);
const queue = new SerialQueue();
let bridgeClient = null;
const referenceStore = new ReferenceStore({
  sessionRoot: path.join(
    os.tmpdir(),
    "visio-scientific-illustrator-test",
    `file-session-${process.pid}-${randomUUID()}`,
  ),
  backend: new BridgeReferenceImageBackend(getBridgeClient),
});

async function getBridgeClient() {
  if (bridgeClient) return bridgeClient;
  const executable = resolveBridgeExecutable(pluginRoot);
  const client = new BridgeClient(executable, ["--mode", "file"], { cwd: pluginRoot });
  await client.start();
  bridgeClient = client;
  return bridgeClient;
}

async function handleTool(name, args) {
  if (name === "visio_test_prepare_reference") {
    return referenceStore.prepareReference(args);
  }
  if (name === "visio_test_get_reference_manifest") {
    return referenceStore.getReferenceManifest(args);
  }
  if (name === "visio_test_get_reference_region") {
    return referenceStore.getReferenceRegion(args);
  }
  if (name === "visio_test_extract_reference_asset") {
    return referenceStore.extractReferenceAsset(args);
  }
  if (name === "visio_test_compare_preview") {
    if (args.heatmap_path && !args.overwrite) {
      try {
        await fs.access(args.heatmap_path);
        const error = new Error(`Output already exists: ${args.heatmap_path}`);
        error.bridgeError = { code: "OUTPUT_EXISTS", message: error.message, retryable: false };
        throw error;
      } catch (error) {
        if (error?.code !== "ENOENT") throw error;
      }
    }
    return comparePngFiles(args.reference_path, args.candidate_path, args.heatmap_path, args.regions);
  }
  const client = await getBridgeClient();
  if (name === "visio_test_validate") return client.call("validate", args);
  if (name === "visio_test_inspect_file") return client.call("inspect_file", args);
  if (name === "visio_test_export") return client.call("export", args, 120000);
  if (name === "visio_test_compile_drawio_scene") return client.call("compile_drawio_scene", args, 120000);
  if (name === "visio_test_validate_fidelity") return client.call("validate_fidelity", args, 120000);
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
      instructions: "Use absolute paths for VSDX validation, draw.io compilation, preview comparison, fidelity inspection, and export.",
    });
  }
  if (method === "ping") return rpcResult(id, {});
  if (method === "tools/list") return rpcResult(id, { tools: fileTools });
  if (method === "tools/call") {
    try {
      const descriptor = findTool(fileTools, params?.name);
      if (!descriptor) {
        return rpcResult(id, toolResult({
          error: "UNSUPPORTED_OPERATION",
          message: `不支持工具: ${params?.name}`,
          retryable: false,
          tool: params?.name,
        }, { isError: true }));
      }
      validateSchema(params?.arguments || {}, descriptor.inputSchema);
      const value = await handleTool(params?.name, params?.arguments || {});
      return rpcResult(id, toolResult(value, { isError: Boolean(value?.error) }));
    } catch (error) {
      return rpcResult(id, toolResult(mapToolError(error, params?.name), { isError: true }));
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
