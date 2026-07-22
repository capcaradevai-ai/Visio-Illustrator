export const launchSchema = {
  type: "object",
  required: ["mode"],
  properties: {
    mode: { type: "string", enum: ["new", "open"] },
    file_path: { type: "string" },
    page_name: { type: "string", default: "Figure" },
    step_delay_ms: {
      type: "integer",
      minimum: 100,
      maximum: 10000,
      default: 500,
    },
    maximize: { type: "boolean", default: true },
    include_preview: { type: "boolean", default: true },
  },
  additionalProperties: false,
};

export const liveTools = [{
  name: "visio_live_launch",
  description: "Launch a plugin-owned visible Microsoft Visio document.",
  inputSchema: launchSchema,
}, {
  name: "visio_live_status",
  description: "Report the state of the plugin-owned Microsoft Visio session.",
  inputSchema: {
    type: "object",
    properties: {},
    additionalProperties: false,
  },
}];

export const fileTools = [{
  name: "visio_file_validate",
  description: "Validate an editable Microsoft Visio VSDX document.",
  inputSchema: {
    type: "object",
    required: ["input_path"],
    properties: {
      input_path: { type: "string" },
    },
    additionalProperties: false,
  },
}];
