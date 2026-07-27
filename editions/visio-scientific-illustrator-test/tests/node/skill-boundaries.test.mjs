import assert from "node:assert/strict";
import test from "node:test";
import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const editionRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const pluginRoot = path.join(
  editionRoot,
  "plugins/visio-scientific-illustrator-test",
);
const skillsRoot = path.join(pluginRoot, "skills");
const referencesRoot = path.join(pluginRoot, "references");

const roles = {
  "visio-test-orchestrate-scientific-figure": {
    boundary: "Recommended entry point",
    displayName: "Visio Test Figure Orchestrator",
  },
  "visio-test-design-scientific-figure": {
    boundary: "Do not execute drawing operations.",
    displayName: "Visio Test Figure Designer",
  },
  "visio-test-draw-scientific-figure-live": {
    boundary: "Do not approve your own result.",
    displayName: "Visio Test Live Figure Drawer",
  },
  "visio-test-audit-scientific-figure": {
    boundary: "Do not modify the Visio page.",
    displayName: "Visio Test Figure Reviewer",
  },
  "visio-test-correct-scientific-figure": {
    boundary: "Do not approve your own correction.",
    displayName: "Visio Test Figure Corrector",
  },
};

const referenceNames = [
  "component-contracts.md",
  "finding-taxonomy.md",
  "quality-contract.md",
  "quality-profiles.md",
  "raster-assets.md",
  "tool-workflows.md",
];

function parseFrontmatter(markdown) {
  const match = markdown.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  assert.ok(match, "SKILL.md must begin with YAML frontmatter");
  const entries = Object.fromEntries(
    match[1]
      .split(/\r?\n/)
      .filter(Boolean)
      .map((line) => {
        const separator = line.indexOf(":");
        assert.notEqual(separator, -1, `invalid frontmatter line: ${line}`);
        return [
          line.slice(0, separator).trim(),
          line.slice(separator + 1).trim(),
        ];
      }),
  );
  return entries;
}

async function loadRole(roleName) {
  const roleRoot = path.join(skillsRoot, roleName);
  const [skill, agent] = await Promise.all([
    readFile(path.join(roleRoot, "SKILL.md"), "utf8"),
    readFile(path.join(roleRoot, "agents/openai.yaml"), "utf8"),
  ]);
  return { skill, agent };
}

test("Test Edition exposes exactly five independent role skills", async () => {
  const entries = await readdir(skillsRoot, { withFileTypes: true });
  const directories = entries
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
  assert.deepEqual(directories, Object.keys(roles).sort());
});

test("role frontmatter, UI metadata, and separation-of-duty boundaries are explicit", async () => {
  for (const [roleName, expected] of Object.entries(roles)) {
    const { skill, agent } = await loadRole(roleName);
    const frontmatter = parseFrontmatter(skill);

    assert.deepEqual(Object.keys(frontmatter).sort(), ["description", "name"]);
    assert.equal(frontmatter.name, roleName);
    assert.match(frontmatter.description, /Visio Scientific Illustrator Test/);
    assert.match(skill, new RegExp(expected.boundary.replaceAll(".", "\\.")));
    assert.match(agent, new RegExp(`display_name: "${expected.displayName}"`));
    assert.match(agent, new RegExp(`\\$${roleName}\\b`));
  }
});

test("skills use progressive disclosure and enforce the Bridge region gate", async () => {
  for (const roleName of Object.keys(roles)) {
    const { skill } = await loadRole(roleName);
    assert.match(skill, /Read only the references required/);
    assert.match(skill, /\.\.\/\.\.\/references\/raster-assets\.md/);
    assert.ok(
      skill.split(/\r?\n/).length <= 180,
      `${roleName} must stay concise`,
    );
  }

  const { skill: orchestrator } = await loadRole(
    "visio-test-orchestrate-scientific-figure",
  );
  assert.match(
    orchestrator,
    /Every logical region must pass the Bridge state machine before the next region can start\./,
  );
  assert.match(orchestrator, /state_handle/);
  assert.match(orchestrator, /finding_id/);
  assert.doesNotMatch(orchestrator, /copy the complete SceneGraph/i);
});

test("all skill and reference tool identifiers are Test-prefixed Visio tools", async () => {
  const documents = [];
  for (const roleName of Object.keys(roles)) {
    const { skill } = await loadRole(roleName);
    documents.push([`${roleName}/SKILL.md`, skill]);
  }
  for (const referenceName of referenceNames) {
    documents.push([
      `references/${referenceName}`,
      await readFile(path.join(referencesRoot, referenceName), "utf8"),
    ]);
  }

  for (const [name, content] of documents) {
    const toolIdentifiers = content.match(/\bvisio_[a-z0-9_]+\b/g) ?? [];
    assert.ok(toolIdentifiers.length > 0, `${name} must name a Test tool`);
    for (const identifier of toolIdentifiers) {
      assert.ok(
        identifier.startsWith("visio_test_"),
        `${name} contains non-Test tool ${identifier}`,
      );
    }

    for (const forbidden of [
      /\bmxCell\b/i,
      /\bmxGraph\b/i,
      /\bgraph model cell\b/i,
      /\bshape tree\b/i,
      /\bslide\b/i,
    ]) {
      assert.doesNotMatch(content, forbidden, `${name}: ${forbidden}`);
    }
  }
});

test("six shared references exist without placeholder content", async () => {
  const entries = await readdir(referencesRoot, { withFileTypes: true });
  const files = entries
    .filter((entry) => entry.isFile())
    .map((entry) => entry.name)
    .sort();
  assert.deepEqual(files, referenceNames);

  for (const referenceName of referenceNames) {
    const content = await readFile(
      path.join(referencesRoot, referenceName),
      "utf8",
    );
    assert.match(content, /^# /);
    assert.doesNotMatch(content, /\b(?:TODO|TBD|PLACEHOLDER)\b/i);
  }
});
