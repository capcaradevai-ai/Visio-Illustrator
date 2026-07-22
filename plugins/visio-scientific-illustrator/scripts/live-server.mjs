#!/usr/bin/env node

import { createInterface } from "node:readline";
import { readFile, unlink, rmdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { BridgeClient, resolveBridgeExecutable } from "./bridge-client.mjs";
import {
  rpcError,
  rpcResult,
  mapSequenceError,
  mapToolError,
  SerialQueue,
  toolResult,
  validateSchema,
} from "./mcp-common.mjs";
import { findTool, liveTools } from "./tool-schemas.mjs";

const serverName = "visio-live";
const serverVersion = "0.1.0";
const supportedProtocols = new Set(["2024-11-05", "2025-03-26", "2025-06-18"]);
const queue = new SerialQueue();
const pluginRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
let bridgeClient = null;
let sessionTempRoot = null;
let defaultStepDelayMs = 500;

async function getBridgeClient() {
  if (bridgeClient) return bridgeClient;
  const executable = resolveBridgeExecutable(pluginRoot);
  const client = new BridgeClient(executable, ["--mode", "live"], { cwd: pluginRoot });
  await client.start();
  bridgeClient = client;
  return bridgeClient;
}

async function handleTool(name, args) {
  const client = await getBridgeClient();
  if (name === "visio_live_launch") {
    const canvas = args.canvas || {};
    const result = args.mode === "open"
      ? await client.call("open_document", {
        file_path: args.file_path,
        ...(args.page_name === undefined ? {} : { page_name: args.page_name }),
        dpi: canvas.dpi ?? 96,
        maximize: args.maximize ?? true,
      })
      : await client.call("launch_new", {
        page_name: args.page_name ?? "Figure",
        width_px: canvas.width_px ?? 1600,
        height_px: canvas.height_px ?? 1000,
        dpi: canvas.dpi ?? 96,
        maximize: args.maximize ?? true,
      });
    sessionTempRoot = path.resolve(result.sessionTempRoot);
    defaultStepDelayMs = args.step_delay_ms ?? 500;
    if (args.include_preview === false) return result;
    const preview = await readOwnedPreview(await client.call("preview", { width_px: 2000 }));
    return { value: { ...result, preview: preview.value }, imageData: preview.imageData };
  }
  if (name === "visio_live_status") return client.call("status", args);
  if (name === "visio_live_clear") return client.call("clear", args);
  if (name === "visio_live_add_shape") return client.call("add_shape", args);
  if (name === "visio_live_add_image") return client.call("add_image", args);
  if (name === "visio_live_add_path") return client.call("add_path", args);
  if (name === "visio_live_add_connector") return client.call("add_connector", args);
  if (name === "visio_live_update_shape") return client.call("update_shape", args);
  if (name === "visio_live_update_path") return client.call("update_path", args);
  if (name === "visio_live_delete_shape") return client.call("delete_shape", args);
  if (name === "visio_live_inspect") return client.call("inspect", args);
  if (name === "visio_live_fit") return client.call("fit", args);
  if (name === "visio_live_preview") {
    return readOwnedPreview(await client.call("preview", args));
  }
  if (name === "visio_live_draw_sequence") return drawSequence(client, args);
  if (name === "visio_live_save") return client.call("save", args);
  if (name === "visio_live_close_document") return client.call("close_document", args);
  if (name === "visio_live_shutdown") return client.call("shutdown", args);
  if (name === "visio_live_group_shapes") return client.call("group_shapes", args);
  if (name === "visio_live_ungroup_shape") return client.call("ungroup_shape", args);
  if (name === "visio_live_add_to_container") return client.call("add_to_container", args);
  if (name === "visio_live_set_z_order") return client.call("set_z_order", args);
  if (name === "visio_live_assign_layer") return client.call("assign_layer", args);
  if (name === "visio_live_align_shapes") return client.call("align_shapes", args);
  if (name === "visio_live_distribute_shapes") return client.call("distribute_shapes", args);
  if (name === "visio_live_match_size") return client.call("match_size", args);
  if (name === "visio_live_set_protection") return client.call("set_protection", args);
  if (name === "visio_live_duplicate_shape") return client.call("duplicate_shape", args);
  if (name === "visio_live_add_scientific_component") return client.call("add_scientific_component", args);
  if (name === "visio_live_get_cells") return client.call("get_cells", args);
  if (name === "visio_live_set_cells") return client.call("set_cells", args);
  if (name === "visio_live_add_connection_point") return client.call("add_connection_point", args);
  if (name === "visio_live_get_geometry") return client.call("get_geometry", args);
  if (name === "visio_live_draw_scene") return drawScene(client, args);
  return { error: "UNSUPPORTED_OPERATION", message: `不支持工具: ${name}`, tool: name };
}

async function readOwnedPreview(preview) {
  if (!sessionTempRoot) throw new Error("SESSION_TEMP_ROOT_NOT_SET");
  const absolutePath = path.resolve(preview.absolutePath);
  const relative = path.relative(sessionTempRoot, absolutePath);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error("PREVIEW_PATH_OUTSIDE_SESSION");
  }
  const imageData = (await readFile(absolutePath)).toString("base64");
  await unlink(absolutePath);
  await rmdir(path.dirname(absolutePath));
  return {
    value: { ...preview, absolutePath: null, consumed: true },
    imageData,
  };
}

async function drawSequence(client, args) {
  const operations = args.operations || [];
  const stepDelayMs = args.step_delay_ms ?? defaultStepDelayMs;
  const results = [];
  for (let index = 0; index < operations.length; index += 1) {
    const { type, ...parameters } = operations[index];
    try {
      if (type === "wait") {
        await delay(parameters.duration_ms ?? stepDelayMs);
        results.push({ waited_ms: parameters.duration_ms ?? stepDelayMs });
      } else {
        const command = {
          shape: "add_shape",
          image: "add_image",
          path: "add_path",
          connector: "add_connector",
          update: "update_shape",
          path_update: "update_path",
          delete: "delete_shape",
          fit: "fit",
        }[type];
        if (!command) throw new Error(`UNSUPPORTED_SEQUENCE_OPERATION: ${type}`);
        results.push(await client.call(command, parameters));
        await delay(stepDelayMs);
      }
    } catch (error) {
      return {
        applied_count: results.length,
        failed_index: index,
        results,
        error: mapSequenceError(error),
      };
    }
  }
  return { applied_count: results.length, failed_index: null, results, error: null };
}

async function drawScene(client, args) {
  const plan = await client.call("prepare_scene", {
    scene: args.scene,
    ...(args.start_region === undefined ? {} : { start_region: args.start_region }),
    ...(args.end_region === undefined ? {} : { end_region: args.end_region }),
    stop_after_checkpoint: args.stop_after_checkpoint ?? false,
    existing_policy: args.existing_policy ?? "error",
  });
  if (plan.conflicts?.length) {
    return {
      value: {
        scene_id: plan.sceneId,
        applied_count: 0,
        failed_index: null,
        conflicts: plan.conflicts,
        checkpoints: [],
        error: "SCENE_CONFLICT",
      },
    };
  }

  const delayMs = args.step_delay_ms ?? defaultStepDelayMs;
  const results = [];
  const checkpoints = [];
  let lastImageData;
  for (let index = 0; index < plan.steps.length; index += 1) {
    const step = plan.steps[index];
    try {
      const result = await client.call(step.command, step.parameters);
      await client.call("refresh", {});
      results.push({ index, command: step.command, semantic_id: step.semantic_id, result });
      await delay(delayMs);
      if (step.checkpoint_after) {
        const preview = await readOwnedPreview(await client.call("preview", { width_px: 2000 }));
        checkpoints.push({ region_id: step.region_id, preview: preview.value });
        lastImageData = preview.imageData;
        if (args.stop_after_checkpoint === true) break;
      }
    } catch (error) {
      return {
        value: {
          scene_id: plan.sceneId,
          applied_count: results.length,
          failed_index: index,
          conflicts: [],
          checkpoints,
          results,
          error: mapSequenceError(error),
        },
        imageData: lastImageData,
      };
    }
  }
  return {
    value: {
      scene_id: plan.sceneId,
      applied_count: results.length,
      failed_index: null,
      conflicts: [],
      checkpoints,
      results,
      error: null,
    },
    imageData: lastImageData,
  };
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

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
    try {
      const descriptor = findTool(liveTools, params?.name);
      if (!descriptor) {
        return rpcResult(id, toolResult({
          error: "UNSUPPORTED_OPERATION",
          message: `不支持工具: ${params?.name}`,
          retryable: false,
          tool: params?.name,
        }, { isError: true }));
      }
      validateSchema(params?.arguments || {}, descriptor.inputSchema);
      const handled = await handleTool(params?.name, params?.arguments || {});
      const value = handled?.value ?? handled;
      return rpcResult(id, toolResult(value, {
        imageData: handled?.imageData,
        isError: Boolean(value?.error),
      }));
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
