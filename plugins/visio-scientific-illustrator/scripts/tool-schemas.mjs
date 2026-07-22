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

const richText = {
  type: "object",
  required: ["text"],
  properties: {
    text: { type: "string", maxLength: 10000 },
    paragraph: {
      type: "object",
      properties: {
        horizontal_align: { type: "string", enum: ["left", "center", "right", "justify"] },
        vertical_align: { type: "string", enum: ["top", "middle", "bottom"] },
        left_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
        right_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
        top_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
        bottom_margin_pt: { type: "number", minimum: 0, maximum: 1000 },
        line_spacing_pt: { type: "number", minimum: -1000, maximum: 1000 },
        space_before_pt: { type: "number", minimum: -1000, maximum: 1000 },
        space_after_pt: { type: "number", minimum: -1000, maximum: 1000 },
      },
      additionalProperties: false,
    },
    runs: {
      type: "array",
      maxItems: 2000,
      items: {
        type: "object",
        required: ["start", "length"],
        properties: {
          start: { type: "integer", minimum: 0 },
          length: { type: "integer", minimum: 1 },
          font_family: { type: "string", minLength: 1, maxLength: 255 },
          font_size_pt: { type: "number", minimum: 1, maximum: 200 },
          color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
          bold: { type: "boolean" },
          italic: { type: "boolean" },
          underline: { type: "boolean" },
          strikethrough: { type: "boolean" },
          superscript: { type: "boolean" },
          subscript: { type: "boolean" },
        },
        additionalProperties: false,
        not: {
          properties: { superscript: { const: true }, subscript: { const: true } },
          required: ["superscript", "subscript"],
        },
      },
    },
    fit: { type: "string", enum: ["none", "shrink_text", "resize_shape"] },
    minimum_font_size_pt: { type: "number", minimum: 1, maximum: 200 },
  },
  additionalProperties: false,
};

const localCoordinate = { type: "number", minimum: 0 };

const pathCommand = {
  oneOf: [
    {
      type: "object",
      required: ["op", "x", "y"],
      properties: { op: { const: "M" }, x: localCoordinate, y: localCoordinate },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["op", "x", "y"],
      properties: { op: { const: "L" }, x: localCoordinate, y: localCoordinate },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["op", "x", "y", "x1", "y1", "x2", "y2"],
      properties: {
        op: { const: "C" },
        x: localCoordinate,
        y: localCoordinate,
        x1: localCoordinate,
        y1: localCoordinate,
        x2: localCoordinate,
        y2: localCoordinate,
      },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["op", "x", "y", "rx", "ry", "rotation_deg", "large_arc", "sweep"],
      properties: {
        op: { const: "A" },
        x: localCoordinate,
        y: localCoordinate,
        rx: { type: "number", exclusiveMinimum: 0 },
        ry: { type: "number", exclusiveMinimum: 0 },
        rotation_deg: { type: "number", minimum: -360, maximum: 360 },
        large_arc: { type: "boolean" },
        sweep: { type: "boolean" },
      },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["op"],
      properties: { op: { const: "Z" } },
      additionalProperties: false,
    },
  ],
};

const pathSection = {
  type: "object",
  required: ["commands"],
  properties: {
    commands: { type: "array", minItems: 1, maxItems: 2000, items: pathCommand },
    closed: { type: "boolean" },
    filled: { type: "boolean" },
  },
  additionalProperties: false,
};

const pathSections = {
  type: "array",
  minItems: 1,
  maxItems: 32,
  items: pathSection,
};

const addPathProperties = {
  semantic_id: semanticId,
  bounds,
  sections: pathSections,
  style: shapeStyle,
};

const updatePathProperties = {
  semantic_id: semanticId,
  bounds,
  sections: pathSections,
  style: shapeStyle,
};

const updatePathRequirement = {
  anyOf: [
    { required: ["bounds"] },
    { required: ["sections"] },
    { required: ["style"] },
  ],
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

const connectorAnchor = {
  oneOf: [
    {
      type: "object",
      required: ["mode"],
      properties: {
        mode: { const: "auto" },
        name: {
          type: "string",
          enum: ["top", "right", "bottom", "left", "top_right", "bottom_right", "bottom_left", "top_left"],
        },
      },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["mode", "row"],
      properties: {
        mode: { const: "connection_point" },
        row: { type: "integer", minimum: 1, maximum: 32767 },
      },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["mode", "name"],
      properties: {
        mode: { const: "connection_point" },
        name: { type: "string", pattern: "^[A-Za-z][A-Za-z0-9_]{0,63}$" },
      },
      additionalProperties: false,
    },
    {
      type: "object",
      required: ["mode", "x", "y"],
      properties: {
        mode: { const: "normalized" },
        x: { type: "number", minimum: 0, maximum: 1 },
        y: { type: "number", minimum: 0, maximum: 1 },
      },
      additionalProperties: false,
    },
  ],
};

const connectorAnchorExclusion = {
  anyOf: [
    { required: ["source_connection_point", "source_anchor"] },
    { required: ["target_connection_point", "target_anchor"] },
  ],
};

const shapeProperties = {
  semantic_id: semanticId,
  shape: {
    type: "string",
    enum: [
      "rectangle", "ellipse", "text", "rounded_rectangle", "diamond",
      "cylinder", "hexagon", "triangle", "parallelogram", "cloud", "container",
      "tensor_face", "video_frame", "latent_vector", "snowflake", "router", "antenna", "stage_panel",
    ],
  },
  bounds,
  label: { type: "string", maxLength: 10000 },
  rich_text: richText,
  style: shapeStyle,
};

const labelRichTextExclusion = { required: ["label", "rich_text"] };

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
  source_anchor: connectorAnchor,
  target_anchor: connectorAnchor,
  jump_style: { type: "string", enum: ["none", "arc", "gap", "square"] },
  label_position: { type: "number", minimum: 0, maximum: 1 },
  avoid_shape_ids: { type: "array", maxItems: 128, items: semanticId },
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
        not: connectorAnchorExclusion,
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
        not: connectorAnchorExclusion,
        additionalProperties: false,
      },
    ],
  };
}

const updateProperties = {
  semantic_id: semanticId,
  label: { type: "string", maxLength: 10000 },
  rich_text: richText,
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
        not: labelRichTextExclusion,
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
      {
        type: "object",
        required: ["type", "semantic_id", "bounds", "sections"],
        properties: { type: { const: "path" }, ...addPathProperties },
        additionalProperties: false,
      },
      connectorSchema({ type: { const: "connector" } }, ["type"]),
      {
        type: "object",
        required: ["type", "semantic_id"],
        properties: { type: { const: "update" }, ...updateProperties },
        not: labelRichTextExclusion,
        additionalProperties: false,
      },
      {
        type: "object",
        required: ["type", "semantic_id"],
        properties: { type: { const: "path_update" }, ...updatePathProperties },
        ...updatePathRequirement,
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

const controlledCellNames = [
  "PinX", "PinY", "Width", "Height", "Angle", "LocPinX", "LocPinY",
  "FillPattern", "FillForegnd", "FillBkgnd", "FillForegndTrans", "FillBkgndTrans",
  "FillGradientDir", "FillGradientAngle", "FillGradientEnabled", "LinePattern", "LineColor",
  "LineWeight", "LineColorTrans", "LineCap", "BeginArrow", "EndArrow", "BeginArrowSize", "EndArrowSize",
  "Rounding", "ShdwPattern", "ShdwForegnd", "ShdwOffsetX", "ShdwOffsetY",
  "Char.Font", "Char.Size", "Char.Color", "Char.Style", "Char.Position", "Para.HorzAlign",
  "Para.IndLeft", "Para.IndRight", "Para.SpBefore", "Para.SpAfter", "Para.SpLine", "VerticalAlign",
  "TxtPinX", "TxtPinY", "TxtWidth", "TxtHeight", "LeftMargin", "RightMargin", "TopMargin", "BottomMargin",
  "LockWidth", "LockHeight", "LockAspect", "LockMoveX", "LockMoveY", "LockRotate",
  "ConFixedCode", "ShapeRouteStyle", "ConLineRouteExt", "LayerMember", "NoObjHandles", "NoCtlHandles", "NoAlignBox",
];

const controlledCellValue = {
  oneOf: [
    {
      type: "object", required: ["kind", "number", "unit"],
      properties: { kind: { const: "length" }, number: { type: "number" }, unit: { type: "string", enum: ["pt", "in"] } },
      additionalProperties: false,
    },
    {
      type: "object", required: ["kind", "number", "unit"],
      properties: { kind: { const: "angle" }, number: { type: "number" }, unit: { const: "deg" } },
      additionalProperties: false,
    },
    {
      type: "object", required: ["kind", "number", "unit"],
      properties: { kind: { const: "percent" }, number: { type: "number", minimum: 0, maximum: 100 }, unit: { const: "%" } },
      additionalProperties: false,
    },
    {
      type: "object", required: ["kind", "color"],
      properties: { kind: { const: "color" }, color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" } },
      additionalProperties: false,
    },
    {
      type: "object", required: ["kind", "boolean"],
      properties: { kind: { const: "boolean" }, boolean: { type: "boolean" } },
      additionalProperties: false,
    },
    {
      type: "object", required: ["kind", "integer"],
      properties: { kind: { const: "integer" }, integer: { type: "integer" } },
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
      not: labelRichTextExclusion,
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
    name: "visio_live_add_path",
    description: "Add editable M/L/C/A/Z geometry as one Visio shape with up to 32 Geometry sections.",
    inputSchema: {
      type: "object",
      required: ["semantic_id", "bounds", "sections"],
      properties: addPathProperties,
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
      not: labelRichTextExclusion,
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_update_path",
    description: "Update editable Geometry sections, bounds, or style without replacing the Visio shape.",
    inputSchema: {
      type: "object",
      required: ["semantic_id"],
      properties: updatePathProperties,
      ...updatePathRequirement,
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
  {
    name: "visio_live_set_z_order",
    description: "Change a shape's native Visio stacking order and report before/after indices.",
    inputSchema: {
      oneOf: [
        {
          type: "object",
          required: ["semantic_id", "action"],
          properties: {
            semantic_id: semanticId,
            action: { type: "string", enum: ["bring_to_front", "send_to_back", "move_forward", "move_backward"] },
          },
          additionalProperties: false,
        },
        {
          type: "object",
          required: ["semantic_id", "action", "position"],
          properties: {
            semantic_id: semanticId,
            action: { const: "absolute" },
            position: { type: "integer", minimum: 1 },
          },
          additionalProperties: false,
        },
      ],
    },
  },
  {
    name: "visio_live_assign_layer",
    description: "Assign shapes to a native Visio layer, optionally replacing existing layer membership.",
    inputSchema: {
      type: "object",
      required: ["semantic_ids", "layer"],
      properties: {
        semantic_ids: { type: "array", minItems: 1, maxItems: 500, items: semanticId },
        layer: { type: "string", minLength: 1, maxLength: 255 },
        create_if_missing: { type: "boolean", default: true },
        replace_existing: { type: "boolean", default: false },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_align_shapes",
    description: "Align editable shapes in page coordinates, including members of groups.",
    inputSchema: {
      type: "object",
      required: ["semantic_ids", "alignment"],
      properties: {
        semantic_ids: { type: "array", minItems: 2, maxItems: 500, items: semanticId },
        alignment: { type: "string", enum: ["left", "center", "right", "top", "middle", "bottom"] },
        reference_semantic_id: semanticId,
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_distribute_shapes",
    description: "Distribute at least three shapes by equal page-space gaps or centers.",
    inputSchema: {
      type: "object",
      required: ["semantic_ids", "axis", "distribution"],
      properties: {
        semantic_ids: { type: "array", minItems: 3, maxItems: 500, items: semanticId },
        axis: { type: "string", enum: ["horizontal", "vertical"] },
        distribution: { type: "string", enum: ["equal_gap", "equal_center"] },
        preserve_outer_bounds: { type: "boolean", default: true },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_match_size",
    description: "Match width, height, or both dimensions to a reference shape in page coordinates.",
    inputSchema: {
      type: "object",
      required: ["semantic_ids", "reference_semantic_id", "dimension"],
      properties: {
        semantic_ids: { type: "array", minItems: 2, maxItems: 500, items: semanticId },
        reference_semantic_id: semanticId,
        dimension: { type: "string", enum: ["width", "height", "both"] },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_set_protection",
    description: "Set explicit Visio ShapeSheet Lock cells without changing unspecified protection fields.",
    inputSchema: {
      type: "object",
      required: ["semantic_ids", "fields"],
      properties: {
        semantic_ids: { type: "array", minItems: 1, maxItems: 500, items: semanticId },
        fields: {
          type: "object",
          properties: {
            move_x: { type: "boolean" }, move_y: { type: "boolean" },
            width: { type: "boolean" }, height: { type: "boolean" }, rotate: { type: "boolean" },
            text_edit: { type: "boolean" }, delete: { type: "boolean" }, format: { type: "boolean" },
            group: { type: "boolean" }, select: { type: "boolean" }, aspect: { type: "boolean" },
            vertex_edit: { type: "boolean" }, begin: { type: "boolean" }, end: { type: "boolean" },
          },
          anyOf: ["move_x", "move_y", "width", "height", "rotate", "text_edit", "delete", "format", "group", "select", "aspect", "vertex_edit", "begin", "end"]
            .map((name) => ({ required: [name] })),
          additionalProperties: false,
        },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_duplicate_shape",
    description: "Duplicate an editable Visio shape or group and rewrite copied semantic IDs.",
    inputSchema: {
      type: "object",
      required: ["semantic_id", "new_semantic_id"],
      properties: {
        semantic_id: semanticId,
        new_semantic_id: semanticId,
        offset_x: { type: "number", default: 20 },
        offset_y: { type: "number", default: 20 },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_add_scientific_component",
    description: "Build a parameterized scientific illustration as grouped, individually editable Visio primitives.",
    inputSchema: {
      type: "object",
      required: ["semantic_id", "component", "bounds"],
      properties: {
        semantic_id: semanticId,
        component: {
          type: "string",
          enum: [
            "number_badge", "router", "human_silhouette", "joint_skeleton",
            "video_frame_stack", "tensor_cube", "latent_vector", "waveform",
            "snowflake", "wireless_link", "stage_panel",
          ],
        },
        bounds,
        options: {
          type: "object",
          properties: {
            title: { type: "string", maxLength: 500 },
            label: { type: "string", maxLength: 100 },
            primary_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
            secondary_color: { type: "string", pattern: "^#[0-9A-Fa-f]{6}$" },
            rows: { type: "integer", minimum: 2, maximum: 100 },
            columns: { type: "integer", minimum: 2, maximum: 100 },
            count: { type: "integer", minimum: 1, maximum: 30 },
            show_labels: { type: "boolean" },
            image_path: absolutePath("png|jpg|jpeg|svg|emf", "Optional local image inside a video-frame component."),
          },
          additionalProperties: false,
        },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_get_cells",
    description: "Read allowlisted universal Visio ShapeSheet cells without evaluating arbitrary formulas.",
    inputSchema: {
      type: "object", required: ["semantic_id", "cells"],
      properties: {
        semantic_id: semanticId,
        cells: { type: "array", minItems: 1, maxItems: 128, items: { type: "string", enum: controlledCellNames } },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_set_cells",
    description: "Write typed values to allowlisted universal Visio ShapeSheet cells; raw formulas are not accepted.",
    inputSchema: {
      type: "object", required: ["semantic_id", "writes"],
      properties: {
        semantic_id: semanticId,
        writes: {
          type: "array", minItems: 1, maxItems: 128,
          items: {
            type: "object", required: ["cell", "value"],
            properties: { cell: { type: "string", enum: controlledCellNames }, value: controlledCellValue },
            additionalProperties: false,
          },
        },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_add_connection_point",
    description: "Add or update a stable named Visio connection point using normalized local coordinates.",
    inputSchema: {
      type: "object", required: ["semantic_id", "name", "x", "y"],
      properties: {
        semantic_id: semanticId,
        name: { type: "string", minLength: 1, maxLength: 64, pattern: "^[A-Za-z][A-Za-z0-9_]*$" },
        x: { type: "number", minimum: 0, maximum: 1 }, y: { type: "number", minimum: 0, maximum: 1 },
        direction_x: { type: "number", minimum: -1, maximum: 1, default: 0 },
        direction_y: { type: "number", minimum: -1, maximum: 1, default: 0 },
      },
      additionalProperties: false,
    },
  },
  {
    name: "visio_live_get_geometry",
    description: "Inspect native Visio Geometry sections and editable row formulas for a shape.",
    inputSchema: {
      type: "object", required: ["semantic_id"],
      properties: { semantic_id: semanticId, include_rows: { type: "boolean", default: true } },
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
