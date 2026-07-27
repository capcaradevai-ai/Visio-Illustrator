import { randomUUID } from "node:crypto";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";

export class RunTelemetry {
  constructor() {
    this.runId = randomUUID();
    this.startedAt = new Date().toISOString();
    this.startedMilliseconds = Date.now();
    this.mcpCallCount = 0;
    this.toolResponseBytes = 0;
    this.previewContextCount = 0;
    this.modelRegionReviewCount = 0;
    this.modelFullPageReviewCount = 0;
    this.safePatchCount = 0;
    this.findingCounts = { hard: 0, warning: 0, info: 0 };
  }

  record(toolName, value) {
    this.mcpCallCount += 1;
    this.toolResponseBytes += Buffer.byteLength(JSON.stringify(value ?? null), "utf8");
    if (toolName === "visio_test_live_preview") this.previewContextCount += 1;
    if (toolName === "visio_test_live_audit_figure") {
      this.findingCounts = {
        hard: value?.hard_count ?? 0,
        warning: value?.warning_count ?? 0,
        info: value?.info_count ?? 0,
      };
    }
    if (toolName === "visio_test_live_apply_safe_patch") this.safePatchCount += 1;
  }

  snapshot() {
    return {
      run_id: this.runId,
      started_at: this.startedAt,
      elapsed_ms: Date.now() - this.startedMilliseconds,
      mcp_call_count: this.mcpCallCount,
      tool_response_bytes: this.toolResponseBytes,
      estimated_tool_tokens: Math.ceil(this.toolResponseBytes / 4),
      estimated: true,
      preview_context_count: this.previewContextCount,
      model_region_review_count: this.modelRegionReviewCount,
      model_full_page_review_count: this.modelFullPageReviewCount,
      safe_patch_count: this.safePatchCount,
      finding_counts: this.findingCounts,
    };
  }

  async finalize(sessionRoot, values) {
    if (!sessionRoot || !path.isAbsolute(sessionRoot)) {
      throw new Error("SESSION_TEMP_ROOT_NOT_SET");
    }
    const profile = requiredString(values.profile, "profile");
    const modelName = requiredString(values.model_name, "model_name");
    const reasoningLevel = requiredString(values.reasoning_level, "reasoning_level");
    this.modelRegionReviewCount = requiredInteger(
      values.model_region_review_count,
      "model_region_review_count",
    );
    this.modelFullPageReviewCount = requiredInteger(
      values.model_full_page_review_count,
      "model_full_page_review_count",
    );
    const result = {
      ...this.snapshot(),
      profile,
      model_name: modelName,
      reasoning_level: reasoningLevel,
      input_tokens: optionalInteger(values.input_tokens, "input_tokens"),
      output_tokens: optionalInteger(values.output_tokens, "output_tokens"),
      structure_score: score(values.structure_score, "structure_score"),
      glue_score: score(values.glue_score, "glue_score"),
      editability_score: score(values.editability_score, "editability_score"),
      visual_score: score(values.visual_score, "visual_score"),
      text_semantic_accuracy: score(
        values.text_semantic_accuracy,
        "text_semantic_accuracy",
      ),
      complete: Number.isInteger(values.input_tokens)
        && Number.isInteger(values.output_tokens),
      finalized_at: new Date().toISOString(),
    };
    const directory = path.join(sessionRoot, "metrics");
    await mkdir(directory, { recursive: true });
    const outputPath = path.join(directory, `${this.runId}.json`);
    await writeFile(outputPath, `${JSON.stringify(result, null, 2)}\n`, {
      encoding: "utf8",
      flag: "wx",
    });
    return { ...result, output_path: outputPath };
  }
}

function requiredString(value, name) {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`${name} 不能为空。`);
  }
  return value.trim();
}

function optionalInteger(value, name) {
  if (value === undefined) return null;
  if (!Number.isInteger(value) || value < 0) throw new Error(`${name} 必须是非负整数。`);
  return value;
}

function requiredInteger(value, name) {
  if (!Number.isInteger(value) || value < 0) {
    throw new Error(`${name} 必须是非负整数。`);
  }
  return value;
}

function score(value, name) {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 0 || value > 1) {
    throw new Error(`${name} 必须在 0 到 1 之间。`);
  }
  return value;
}
