#!/usr/bin/env node

import { createInterface } from "node:readline";
import { createHash } from "node:crypto";
import {
  compactAuditResult,
  compactFindingList,
} from "./audit-response.mjs";
import {
  computeRegionalVisualScore,
  loadAuditReference,
} from "./audit-reference.mjs";
import { readFile, unlink, rmdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { BridgeClient, resolveBridgeExecutable } from "./bridge-client.mjs";
import { RunTelemetry } from "./telemetry.mjs";
import { comparePngFiles } from "./visual-compare.mjs";
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

const serverName = "visio-test-live";
const pluginRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const serverVersion = JSON.parse(await readFile(path.join(pluginRoot, ".codex-plugin", "plugin.json"), "utf8")).version;
const supportedProtocols = new Set(["2024-11-05", "2025-03-26", "2025-06-18"]);
const queue = new SerialQueue();
let bridgeClient = null;
let sessionTempRoot = null;
let defaultStepDelayMs = 500;
let pendingRegionGate = null;
let telemetry = new RunTelemetry();

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
  if (name === "visio_test_live_launch") {
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
    pendingRegionGate = null;
    telemetry = new RunTelemetry();
    sessionTempRoot = path.resolve(result.sessionTempRoot);
    defaultStepDelayMs = args.step_delay_ms ?? 500;
    if (args.include_preview === false) return result;
    const preview = await readOwnedPreview(await client.call("preview", { width_px: 2000 }));
    return { value: { ...result, preview: preview.value }, imageData: preview.imageData };
  }
  if (name === "visio_test_live_status") return client.call("status", args);
  if (name === "visio_test_live_get_capabilities") {
    return client.call("get_capabilities", args);
  }
  if (name === "visio_test_live_get_capability_details") {
    return client.call("get_capability_details", args);
  }
  if (name === "visio_test_live_clear") return client.call("clear", args);
  if (name === "visio_test_live_add_shape") return client.call("add_shape", args);
  if (name === "visio_test_live_add_image") return client.call("add_image", args);
  if (name === "visio_test_live_add_path") return client.call("add_path", args);
  if (name === "visio_test_live_add_connector") return client.call("add_connector", args);
  if (name === "visio_test_live_add_table") return client.call("add_table", args);
  if (name === "visio_test_live_add_chart") return client.call("add_chart", args);
  if (name === "visio_test_live_add_matrix") return client.call("add_matrix", args);
  if (name === "visio_test_live_add_grid") return client.call("add_grid", args);
  if (name === "visio_test_live_update_table_cell") {
    return client.call("update_table_cell", args);
  }
  if (name === "visio_test_live_update_table_layout") {
    return client.call("update_table_layout", args);
  }
  if (name === "visio_test_live_update_chart") return client.call("update_chart", args);
  if (name === "visio_test_live_update_matrix_cell") {
    return client.call("update_matrix_cell", args);
  }
  if (name === "visio_test_live_update_grid") return client.call("update_grid", args);
  if (name === "visio_test_live_audit_figure") return auditFigure(client, args);
  if (name === "visio_test_live_get_audit_findings") {
    return compactFindingList(await client.call("get_audit_findings", args));
  }
  if (name === "visio_test_live_get_audit_finding") {
    return client.call("get_audit_finding", args);
  }
  if (name === "visio_test_live_set_audit_decision") {
    return client.call("set_audit_decision", args);
  }
  if (name === "visio_test_live_apply_safe_patch") {
    return client.call("apply_safe_patch", args);
  }
  if (name === "visio_test_live_update_shape") return client.call("update_shape", args);
  if (name === "visio_test_live_update_path") return client.call("update_path", args);
  if (name === "visio_test_live_delete_shape") return client.call("delete_shape", args);
  if (name === "visio_test_live_inspect") return client.call("inspect", args);
  if (name === "visio_test_live_fit") return client.call("fit", args);
  if (name === "visio_test_live_preview") {
    return readOwnedPreview(await client.call("preview", args));
  }
  if (name === "visio_test_live_draw_sequence") return drawSequence(client, args);
  if (name === "visio_test_live_save") return client.call("save", args);
  if (name === "visio_test_live_close_document") {
    const result = await client.call("close_document", args);
    resetSessionState();
    return result;
  }
  if (name === "visio_test_live_shutdown") {
    const result = await client.call("shutdown", args);
    resetSessionState();
    return result;
  }
  if (name === "visio_test_live_group_shapes") return client.call("group_shapes", args);
  if (name === "visio_test_live_ungroup_shape") return client.call("ungroup_shape", args);
  if (name === "visio_test_live_add_to_container") return client.call("add_to_container", args);
  if (name === "visio_test_live_set_z_order") return client.call("set_z_order", args);
  if (name === "visio_test_live_assign_layer") return client.call("assign_layer", args);
  if (name === "visio_test_live_align_shapes") return client.call("align_shapes", args);
  if (name === "visio_test_live_distribute_shapes") return client.call("distribute_shapes", args);
  if (name === "visio_test_live_match_size") return client.call("match_size", args);
  if (name === "visio_test_live_set_protection") return client.call("set_protection", args);
  if (name === "visio_test_live_duplicate_shape") return client.call("duplicate_shape", args);
  if (name === "visio_test_live_add_scientific_component") return client.call("add_scientific_component", args);
  if (name === "visio_test_live_get_cells") return client.call("get_cells", args);
  if (name === "visio_test_live_set_cells") return client.call("set_cells", args);
  if (name === "visio_test_live_add_connection_point") return client.call("add_connection_point", args);
  if (name === "visio_test_live_get_geometry") return client.call("get_geometry", args);
  if (name === "visio_test_live_get_run_metrics") return telemetry.snapshot();
  if (name === "visio_test_live_finalize_run_metrics") {
    return telemetry.finalize(sessionTempRoot, args);
  }
  if (name === "visio_test_live_draw_scene") return drawScene(client, args);
  return { error: "UNSUPPORTED_OPERATION", message: `不支持工具: ${name}`, tool: name };
}

function resetSessionState() {
  sessionTempRoot = null;
  defaultStepDelayMs = 500;
  pendingRegionGate = null;
}

async function readOwnedPreview(preview) {
  const absolutePath = resolveOwnedPreviewPath(preview);
  const imageData = (await readFile(absolutePath)).toString("base64");
  const previewSha256 = createHash("sha256")
    .update(Buffer.from(imageData, "base64"))
    .digest("hex");
  await unlink(absolutePath);
  await rmdir(path.dirname(absolutePath));
  return {
    value: {
      ...preview,
      absolutePath: null,
      consumed: true,
      preview_sha256: previewSha256,
    },
    imageData,
  };
}

function resolveOwnedPreviewPath(preview) {
  if (!sessionTempRoot) throw new Error("SESSION_TEMP_ROOT_NOT_SET");
  const absolutePath = path.resolve(preview.absolutePath);
  const relative = path.relative(sessionTempRoot, absolutePath);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error("PREVIEW_PATH_OUTSIDE_SESSION");
  }
  return absolutePath;
}

async function auditFigure(client, args) {
  const quality = await client.call("quality_status", {});
  const structure = await client.call("get_structure_hash", {});
  const reference = await loadAuditReference(args);
  const previewDescriptor = await client.call(
    "preview",
    { width_px: args.preview_width_px ?? 2000 },
  );
  const previewPath = resolveOwnedPreviewPath(previewDescriptor);
  const comparison = await comparePngFiles(
    reference.manifest.source_path,
    previewPath,
    null,
    [{
      id: reference.region.id,
      ...reference.region.bounds,
    }],
  );
  const regionalVisualScore = computeRegionalVisualScore(comparison.regions[0]);
  const preview = await readOwnedPreview(
    previewDescriptor,
  );
  const result = await client.call("audit_figure", {
    page_revision: quality.page_revision,
    region_id: args.region_id,
    evidence: {
      page_revision: quality.page_revision,
      region_id: args.region_id,
      preview_sha256: preview.value.preview_sha256,
      reference_crop_sha256: reference.region_sha256,
      structure_snapshot_sha256: structure.structure_snapshot_sha256,
      audit_profile: args.audit_profile,
    },
    objects: args.objects ?? [],
    regional_visual_score: regionalVisualScore,
    minimum_regional_visual_score: args.minimum_regional_visual_score,
    ...(args.maximum_alignment_error === undefined
      ? {}
      : { maximum_alignment_error: args.maximum_alignment_error }),
    ...(args.minimum_connector_clearance === undefined
      ? {}
      : { minimum_connector_clearance: args.minimum_connector_clearance }),
  });
  return {
    value: compactAuditResult(result),
    ...(result.can_pass ? {} : { imageData: preview.imageData }),
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
  if (pendingRegionGate && args.start_region !== pendingRegionGate) {
    await client.call("assert_quality_gate", { region_id: pendingRegionGate });
    pendingRegionGate = null;
  }
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
        pendingRegionGate = step.region_id;
        break;
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
      quality_gate_required: pendingRegionGate,
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
      if (!params?.name.endsWith("_get_run_metrics")
          && !params?.name.endsWith("_finalize_run_metrics")) {
        telemetry.record(params?.name, value);
      }
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
