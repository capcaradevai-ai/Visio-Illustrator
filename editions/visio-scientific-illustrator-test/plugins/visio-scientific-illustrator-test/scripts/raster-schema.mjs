const hash = {
  type: "string",
  pattern: "^[0-9a-f]{64}$",
};

const bounds = {
  type: "object",
  required: ["x", "y", "width", "height"],
  properties: {
    x: { type: "number", minimum: 0 },
    y: { type: "number", minimum: 0 },
    width: { type: "number", exclusiveMinimum: 0 },
    height: { type: "number", exclusiveMinimum: 0 },
  },
  additionalProperties: false,
};

export const rasterDeclarationSchema = {
  type: "object",
  required: [
    "asset_id",
    "source_type",
    "raster_reason",
    "source_is_tightly_cropped",
    "atomic_raster_unit",
    "contains_reconstructable_content",
    "decomposition_note",
    "editable_scope",
    "asset_sha256",
    "semantic_role",
  ],
  properties: {
    asset_id: { type: "string", minLength: 1, maxLength: 120 },
    source_type: {
      type: "string",
      enum: ["reference_crop", "user_file", "generated_external"],
    },
    raster_reason: { type: "string", minLength: 1, maxLength: 2000 },
    source_is_tightly_cropped: { const: true },
    atomic_raster_unit: { const: true },
    contains_reconstructable_content: { const: false },
    decomposition_note: { type: "string", minLength: 1, maxLength: 2000 },
    editable_scope: { const: "object_only" },
    source_reference_sha256: hash,
    source_bounds: bounds,
    asset_sha256: hash,
    semantic_role: { type: "string", minLength: 1, maxLength: 120 },
    provenance_note: { type: "string", minLength: 1, maxLength: 2000 },
  },
  allOf: [
    {
      if: { properties: { source_type: { const: "reference_crop" } } },
      then: { required: ["source_reference_sha256", "source_bounds"] },
    },
    {
      if: { properties: { source_type: { const: "user_file" } } },
      then: {
        not: {
          anyOf: [
            { required: ["source_reference_sha256"] },
            { required: ["source_bounds"] },
            { required: ["provenance_note"] },
          ],
        },
      },
    },
    {
      if: { properties: { source_type: { const: "generated_external" } } },
      then: {
        required: ["provenance_note"],
        not: {
          anyOf: [
            { required: ["source_reference_sha256"] },
            { required: ["source_bounds"] },
          ],
        },
      },
    },
  ],
  additionalProperties: false,
};
