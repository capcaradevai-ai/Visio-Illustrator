const emptyObject = {
  type: "object",
  properties: {},
  additionalProperties: false,
};

const semanticId = {
  type: "string",
  minLength: 1,
  maxLength: 120,
  description: "Stable ID stored in User.CodexId.",
};

const absolutePath = (extension, description) => ({
  type: "string",
  minLength: 3,
  description: `${description} Must be an absolute Windows .${extension} path.`,
});

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

const fillStyle = {
  type: "object",
  required: ["kind"],
  properties: {
    kind: { type: "string", enum: ["none", "solid", "gradient", "pattern"] },
    color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    secondary_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    transparency_percent: { type: "number", minimum: 0, maximum: 100 },
    secondary_transparency_percent: { type: "number", minimum: 0, maximum: 100 },
    gradient_angle_deg: { type: "number", minimum: -360, maximum: 360 },
    gradient_direction: { type: "integer", minimum: 0, maximum: 7 },
    pattern: { type: "integer", minimum: 2, maximum: 40 },
  },
  additionalProperties: false,
};

const lineStyle = {
  type: "object",
  properties: {
    color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    width_pt: { type: "number", minimum: 0, maximum: 50 },
    transparency_percent: { type: "number", minimum: 0, maximum: 100 },
    pattern: { type: "integer", minimum: 0, maximum: 23 },
    begin_arrow: { type: "string", enum: ["none", "open", "triangle", "diamond", "oval"] },
    end_arrow: { type: "string", enum: ["none", "open", "triangle", "diamond", "oval"] },
    begin_arrow_size: { type: "string", enum: ["small", "medium", "large"] },
    end_arrow_size: { type: "string", enum: ["small", "medium", "large"] },
    cap: { type: "string", enum: ["square", "round", "extended"] },
    join: { type: "string", enum: ["round", "bevel", "miter"] },
    rounded: { type: "boolean" },
  },
  additionalProperties: false,
};

const textBlockStyle = {
  type: "object",
  properties: {
    horizontal_align: { type: "string", enum: ["left", "center", "right", "justify"] },
    vertical_align: { type: "string", enum: ["top", "middle", "bottom"] },
    left_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
    right_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
    top_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
    bottom_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
  },
  additionalProperties: false,
};

const shadowStyle = {
  type: "object",
  required: ["enabled"],
  properties: {
    enabled: { type: "boolean" },
    color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    transparency_percent: { type: "number", minimum: 0, maximum: 100 },
    offset_x_pt: { type: "number", minimum: -1000, maximum: 1000 },
    offset_y_pt: { type: "number", minimum: -1000, maximum: 1000 },
  },
  additionalProperties: false,
};

const legacyStyleFields = [
  "fill_color", "fill_transparency", "stroke_color", "stroke_width_pt",
  "font_color", "font_size_pt", "dashed",
];
const richStyleFields = ["fill", "line", "text_block", "shadow", "corner", "opacity_percent"];

const shapeStyle = {
  type: "object",
  properties: {
    fill_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    fill_transparency: { type: "number", minimum: 0, maximum: 100 },
    stroke_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    stroke_width_pt: { type: "number", minimum: 0, maximum: 50 },
    font_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    font_size_pt: { type: "number", minimum: 1, maximum: 200 },
    rotation_deg: { type: "number", minimum: -360, maximum: 360 },
    dashed: { type: "boolean" },
    fill: fillStyle,
    line: lineStyle,
    text_block: textBlockStyle,
    shadow: shadowStyle,
    corner: {
      type: "object",
      required: ["radius_pt"],
      properties: { radius_pt: { type: "number", minimum: 0, maximum: 1000 } },
      additionalProperties: false,
    },
    opacity_percent: { type: "number", minimum: 0, maximum: 100 },
  },
  not: {
    allOf: [
      { anyOf: legacyStyleFields.map((field) => ({ required: [field] })) },
      { anyOf: richStyleFields.map((field) => ({ required: [field] })) },
    ],
  },
  additionalProperties: false,
};

const connectorStyle = {
  type: "object",
  properties: {
    stroke_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
    stroke_width_pt: { type: "number", minimum: 0, maximum: 50 },
    dashed: { type: "boolean" },
    start_arrow: { type: "string", enum: ["none", "triangle", "open"] },
    end_arrow: { type: "string", enum: ["none", "triangle", "open"] },
  },
  additionalProperties: false,
};

const shapeProperties = {
  semantic_id: semanticId,
  shape: {
    type: "string",
    enum: [
      "rectangle", "ellipse", "text", "rounded_rectangle", "diamond",
      "cylinder", "hexagon", "triangle", "parallelogram", "cloud", "container",
    ],
  },
  bounds,
  label: { type: "string", maxLength: 10000 },
  style: shapeStyle,
};

const waypoint = {
  type: "object",
  required: ["x", "y"],
  properties: {
    x: { type: "number", minimum: 0 },
    y: { type: "number", minimum: 0 },
  },
  additionalProperties: false,
};

const connectorBaseProperties = {
  semantic_id: semanticId,
  source: semanticId,
  target: semanticId,
  label: { type: "string", maxLength: 10000 },
  source_connection_point: { type: "integer", minimum: 1, maximum: 32767 },
  target_connection_point: { type: "integer", minimum: 1, maximum: 32767 },
  style: connectorStyle,
};

function connectorSchema(extraProperties = {}, extraRequired = []) {
  const required = [...extraRequired, "semantic_id", "source", "target"];
  return {
    oneOf: [
      {
        type: "object",
        required: [...required, "routing", "waypoints"],
        properties: {
          ...extraProperties,
          ...connectorBaseProperties,
          routing: { const: "polyline" },
          waypoints: { type: "array", minItems: 1, maxItems: 64, items: waypoint },
        },
        additionalProperties: false,
      },
      {
        type: "object",
        required,
        properties: {
          ...extraProperties,
          ...connectorBaseProperties,
          routing: { type: "string", enum: ["dynamic", "right_angle", "straight"] },
        },
        additionalProperties: false,
      },
    ],
  };
}

const updateProperties = {
  semantic_id: semanticId,
  label: { type: "string", maxLength: 10000 },
  x: { type: "number", minimum: 0 },
  y: { type: "number", minimum: 0 },
  width: { type: "number", exclusiveMinimum: 0 },
  height: { type: "number", exclusiveMinimum: 0 },
  style: shapeStyle,
};

const sequenceOperations = {
  type: "array",
  minItems: 1,
  maxItems: 500,
  items: {
    oneOf: [
      {
        type: "object",
        required: ["type", "semantic_id", "shape", "bounds"],
        properties: { type: { const: "shape" }, ...shapeProperties },
        additionalProperties: false,
      },
      {
        type: "object",
        required: ["type", "semantic_id", "file_path", "bounds"],
        properties: {
          type: { const: "image" },
          semantic_id: semanticId,
          file_path: absolutePath("png/svg/emf", "Local image source."),
          bounds,
        },
        additionalProperties: false,
      },
      connectorSchema({ type: { const: "connector" } }, ["type"]),
      {
        type: "object",
        required: ["type", "semantic_id"],
        properties: { type: { const: "update" }, ...updateProperties },
        additionalProperties: false,
      },
      {
        type: "object",
        required: ["type", "semantic_id", "connector_policy"],
        properties: {
          type: { const: "delete" },
          semantic_id: semanticId,
          connector_policy: { type: "string", enum: ["keep", "delete"] },
        },
        additionalProperties: false,
      },
      {
        type: "object",
        required: ["type", "mode"],
        properties: {
          type: { const: "fit" },
          mode: { type: "string", enum: ["page", "width"] },
        },
        additionalProperties: false,
      },
      {
        type: "object",
        required: ["type"],
        properties: {
          type: { const: "wait" },
          duration_ms: { type: "integer", minimum: 100, maximum: 10000 },
        },
        additionalProperties: false,
      },
    ],
  },
};

const launchCommonProperties = {
  page_name: { type: "string", minLength: 1, maxLength: 255 },
  step_delay_ms: { type: "integer", minimum: 100, maximum: 10000, default: 500 },
  maximize: { type: "boolean", default: true },
  include_preview: { type: "boolean", default: true },
};

export const launchSchema = {
  oneOf: [
    {
      type: "object",
      required: ["mode"],
      properties: {
        mode: { const: "new" },
        ...launchCommonProperties,
        canvas: {
          type: "object",
          properties: {
            width_px: { type: "integer", minimum: 100, maximum: 30000, default: 1600 },
            height_px: { type: "integer", minimum: 100, maximum: 30000, default: 1000 },
            dpi: { type: "integer", minimum: 48, maximum: 600, default: 96 },
          },
          additionalProperties: false,
        },
      },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["mode", "file_path"],
      properties: {
        mode: { const: "open" },
        file_path: absolutePath("vsdx", "Existing editable Visio document."),
        ...launchCommonProperties,
        canvas: {
          type: "object",
          properties: {
            dpi: { type: "integer", minimum: 48, maximum: 600, default: 96 },
          },
          additionalProperties: false,
        },
      },
      additionalProperties: false,
    },
  ],
};

export const liveTools = [
  {
    name: "visio_live_launch",
    description: "Launch a plugin-owned visible Microsoft Visio document.",
    inputSchema: launchSchema,
  },
  {
    name: "visio_live_status",
    description: "Report the plugin-owned Visio session state.",
    inputSchema: emptyObject,
  },
  {
    name: "visio_live_clear",
    description: "Clear the current page as one undoable operation.",
    inputSchema: {
      type: "object",
      required: ["confirm"],
      properties: { confirm: { const: true } },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_add_shape",
    description: "Add an editable 2D Visio shape or text shape.",
    inputSchema: {
      type: "object",
      required: ["semantic_id", "shape", "bounds"],
      properties: shapeProperties,
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_add_image",
    description: "Import a local PNG, JPEG, SVG, or EMF into Visio.",
    inputSchema: {
      type: "object",
      required: ["semantic_id", "file_path", "bounds"],
      properties: {
        semantic_id: semanticId,
        file_path: absolutePath("png/jpeg/svg/emf", "Local image source."),
        bounds,
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_add_connector",
    description: "Add a 1D Visio connector glued to two semantic shapes.",
    inputSchema: connectorSchema(),
  },
  {
    name: "visio_live_update_shape",
    description: "Update only explicitly supplied fields on an existing shape.",
    inputSchema: {
      type: "object",
      required: ["semantic_id"],
      properties: updateProperties,
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_delete_shape",
    description: "Delete a shape with an explicit connected-connector policy.",
    inputSchema: {
      type: "object",
      required: ["semantic_id", "connector_policy"],
      properties: {
        semantic_id: semanticId,
        connector_policy: { type: "string", enum: ["keep", "delete"] },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_draw_sequence",
    description: "Apply visible operations in order and stop at the first failure.",
    inputSchema: {
      type: "object",
      required: ["operations"],
      properties: {
        operations: sequenceOperations,
        step_delay_ms: { type: "integer", minimum: 100, maximum: 10000 },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_inspect",
    description: "Inspect editable shapes and glued connections on the current page.",
    inputSchema: {
      type: "object",
      properties: { max_shapes: { type: "integer", minimum: 1, maximum: 5000, default: 500 } },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_fit",
    description: "Fit the visible drawing window to the page or page width.",
    inputSchema: {
      type: "object",
      required: ["mode"],
      properties: { mode: { type: "string", enum: ["page", "width"] } },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_preview",
    description: "Export the current Visio page as a temporary PNG preview.",
    inputSchema: {
      type: "object",
      properties: { width_px: { type: "integer", minimum: 128, maximum: 10000, default: 2000 } },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_save",
    description: "Safely save and validate an editable VSDX.",
    inputSchema: {
      type: "object",
      required: ["output_path"],
      properties: {
        output_path: absolutePath("vsdx", "Destination for the editable Visio document."),
        overwrite: { type: "boolean", default: false },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_close_document",
    description: "Close the owned document with an explicit dirty action.",
    inputSchema: {
      type: "object",
      required: ["action"],
      properties: { action: { type: "string", enum: ["save", "discard", "cancel"] } },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_shutdown",
    description: "Shut down only the plugin-owned Visio instance.",
    inputSchema: {
      type: "object",
      required: ["confirm", "dirty_action"],
      properties: {
        confirm: { const: true },
        dirty_action: { type: "string", enum: ["save", "discard", "cancel"] },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_group_shapes",
    description: "Group top-level shapes while retaining member semantic IDs.",
    inputSchema: {
      type: "object",
      required: ["group_semantic_id", "member_semantic_ids"],
      properties: {
        group_semantic_id: semanticId,
        member_semantic_ids: {
          type: "array",
          minItems: 2,
          maxItems: 500,
          items: semanticId,
        },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_ungroup_shape",
    description: "Ungroup a Visio group and retain its member semantic IDs.",
    inputSchema: {
      type: "object",
      required: ["group_semantic_id"],
      properties: { group_semantic_id: semanticId },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_add_to_container",
    description: "Add top-level shapes to a real Visio container.",
    inputSchema: {
      type: "object",
      required: ["container_semantic_id", "member_semantic_ids"],
      properties: {
        container_semantic_id: semanticId,
        member_semantic_ids: {
          type: "array",
          minItems: 1,
          maxItems: 500,
          items: semanticId,
        },
      },
      additionalProperties: false,
    },
  },
];

export const fileTools = [
  {
    name: "visio_validate",
    description: "Validate an editable Microsoft Visio VSDX document.",
    inputSchema: {
      type: "object",
      required: ["input_path"],
      properties: { input_path: absolutePath("vsdx", "VSDX to validate.") },
      additionalProperties: false,
    },
  },
  {
    name: "visio_inspect_file",
    description: "Inspect shapes and connections in a saved VSDX.",
    inputSchema: {
      type: "object",
      required: ["input_path"],
      properties: {
        input_path: absolutePath("vsdx", "VSDX to inspect."),
        max_shapes: { type: "integer", minimum: 1, maximum: 5000, default: 500 },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_export",
    description: "Export a VSDX to PNG, SVG, or PDF without changing the source.",
    inputSchema: {
      oneOf: [
        {
          type: "object",
          required: ["input_path", "format", "output_path", "page_scope"],
          properties: {
            input_path: absolutePath("vsdx", "Source VSDX."),
            format: { const: "png" },
            output_path: { type: "string", minLength: 3, description: "Absolute PNG output path." },
            page_scope: { const: "current" },
            overwrite: { type: "boolean", default: false },
            width: { type: "integer", minimum: 128, maximum: 10000, default: 2000 },
          },
          additionalProperties: false,
        },
        {
          type: "object",
          required: ["input_path", "format", "output_path", "page_scope"],
          properties: {
            input_path: absolutePath("vsdx", "Source VSDX."),
            format: { const: "svg" },
            output_path: { type: "string", minLength: 3, description: "Absolute SVG output path." },
            page_scope: { const: "current" },
            overwrite: { type: "boolean", default: false },
          },
          additionalProperties: false,
        },
        {
          type: "object",
          required: ["input_path", "format", "output_path", "page_scope"],
          properties: {
            input_path: absolutePath("vsdx", "Source VSDX."),
            format: { const: "pdf" },
            output_path: { type: "string", minLength: 3, description: "Absolute PDF output path." },
            page_scope: { type: "string", enum: ["current", "all"] },
            overwrite: { type: "boolean", default: false },
          },
          additionalProperties: false,
        },
      ],
    },
  },
];

export function findTool(tools, name) {
  return tools.find((tool) => tool.name === name);
}
