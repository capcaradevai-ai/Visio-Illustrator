#!/usr/bin/env node

import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const editionRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

export async function prepare(configPath, outputRoot = path.join(
  editionRoot,
  "TestResults",
  "quality-ab",
)) {
  const absoluteConfig = path.resolve(configPath);
  const config = JSON.parse(await readFile(absoluteConfig, "utf8"));
  const configDirectory = path.dirname(absoluteConfig);
  const referencePath = resolveExisting(configDirectory, config.reference_path);
  const scenePath = resolveExisting(configDirectory, config.scene_path);
  await mkdir(outputRoot, { recursive: true });
  const common = {
    fixture_id: required(config.fixture_id, "fixture_id"),
    reference_path: referencePath,
    reference_sha256: await hashFile(referencePath),
    scene_path: scenePath,
    scene_sha256: await hashFile(scenePath),
    canvas: config.canvas,
    maximum_regions: 2,
    maximum_corrections_per_region: 1,
    machine_id: createHash("sha256")
      .update(`${process.platform}|${process.arch}|${process.version}`)
      .digest("hex"),
  };
  const runs = ["strict", "adaptive"].map((profile, order) => ({
    run_id: `${common.fixture_id}-${profile}`,
    order: order + 1,
    profile,
    descriptor_path: path.join(outputRoot, `${profile}-descriptor.json`),
    result_path: path.join(outputRoot, `${profile}-result.json`),
  }));
  for (const run of runs) {
    await writeFile(
      run.descriptor_path,
      `${JSON.stringify({ ...common, ...run }, null, 2)}\n`,
      "utf8",
    );
  }
  const manifest = {
    schema_version: "1.0",
    status: "prepared",
    prepared_at: new Date().toISOString(),
    common,
    runs,
  };
  const manifestPath = path.join(outputRoot, "manifest.json");
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
  return { manifest_path: manifestPath, manifest };
}

export async function compare(manifestPath) {
  const absoluteManifest = path.resolve(manifestPath);
  const manifest = JSON.parse(await readFile(absoluteManifest, "utf8"));
  const outputRoot = path.dirname(absoluteManifest);
  const results = [];
  for (const run of manifest.runs) {
    try {
      results.push(JSON.parse(await readFile(run.result_path, "utf8")));
    }
    catch (error) {
      if (error.code !== "ENOENT") throw error;
    }
  }

  const complete = results.length === 2
    && results.every((result) => result.complete === true
      && Number.isInteger(result.input_tokens)
      && Number.isInteger(result.output_tokens));
  const summary = complete
    ? evaluate(results)
    : {
      status: "incomplete",
      default_profile: "strict",
      reason: "缺少两个 finalized run 或真实宿主 token。",
      completed_run_count: results.length,
    };
  const summaryPath = path.join(outputRoot, "summary.json");
  const reportPath = path.join(outputRoot, "report.md");
  await writeFile(summaryPath, `${JSON.stringify(summary, null, 2)}\n`, "utf8");
  await writeFile(reportPath, renderReport(summary), "utf8");
  return { summary_path: summaryPath, report_path: reportPath, summary };
}

function evaluate(results) {
  const strict = results.find((item) => item.profile === "strict");
  const adaptive = results.find((item) => item.profile === "adaptive");
  if (!strict || !adaptive) {
    return {
      status: "incomplete",
      default_profile: "strict",
      reason: "结果缺少 strict 或 adaptive。",
    };
  }
  const checks = {
    model_image_reviews_reduction:
      reduction(
        strict.model_region_review_count + strict.model_full_page_review_count,
        adaptive.model_region_review_count + adaptive.model_full_page_review_count,
      ) >= 0.5,
    tool_token_reduction:
      reduction(strict.estimated_tool_tokens, adaptive.estimated_tool_tokens) >= 0.4,
    elapsed_reduction: reduction(strict.elapsed_ms, adaptive.elapsed_ms) >= 0.3,
    structure_not_lower: adaptive.structure_score >= strict.structure_score,
    glue_not_lower: adaptive.glue_score >= strict.glue_score,
    editability_not_lower: adaptive.editability_score >= strict.editability_score,
    visual_within_ten_percent: adaptive.visual_score >= strict.visual_score * 0.9,
    semantic_exact: adaptive.text_semantic_accuracy === 1,
  };
  const passed = Object.values(checks).every(Boolean);
  return {
    status: passed ? "passed" : "failed",
    default_profile: passed ? "adaptive" : "strict",
    checks,
  };
}

function reduction(original, candidate) {
  if (!Number.isFinite(original) || original <= 0 || !Number.isFinite(candidate)) return 0;
  return (original - candidate) / original;
}

function renderReport(summary) {
  const checks = summary.checks
    ? Object.entries(summary.checks)
      .map(([name, passed]) => `- ${passed ? "PASS" : "FAIL"}: ${name}`)
      .join("\n")
    : `- ${summary.reason}`;
  return `# Visio Test Edition quality A/B\n\n`
    + `Status: ${summary.status}\n\n`
    + `Default profile: ${summary.default_profile}\n\n`
    + `${checks}\n`;
}

async function hashFile(filePath) {
  return createHash("sha256").update(await readFile(filePath)).digest("hex");
}

function resolveExisting(base, relativePath) {
  const result = path.resolve(base, required(relativePath, "path"));
  return result;
}

function required(value, field) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error(`${field} 不能为空。`);
  }
  return value;
}

async function main() {
  const [command, option, value] = process.argv.slice(2);
  if (command === "prepare" && option === "--config" && value) {
    const result = await prepare(value);
    process.stdout.write(`${JSON.stringify(result)}\n`);
    return;
  }
  if (command === "compare" && option === "--manifest" && value) {
    const result = await compare(value);
    process.stdout.write(`${JSON.stringify(result)}\n`);
    return;
  }
  throw new Error("用法：run-quality-ab.mjs prepare --config <path> 或 compare --manifest <path>");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  });
}
