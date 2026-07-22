export class SerialQueue {
  #tail = Promise.resolve();

  run(task) {
    const result = this.#tail.then(task, task);
    this.#tail = result.catch(() => undefined);
    return result;
  }
}

export function validateObject(value, { required = [], allowed = [] }) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("参数必须是对象。");
  }
  for (const key of required) {
    if (!(key in value)) throw new Error(`缺少字段: ${key}`);
  }
  for (const key of Object.keys(value)) {
    if (!allowed.includes(key)) throw new Error(`未知字段: ${key}`);
  }
  return value;
}

export class McpValidationError extends Error {
  constructor(message, fieldPath) {
    super(message);
    this.code = "INVALID_ARGUMENT";
    this.details = { field_path: fieldPath };
  }
}

export function validateSchema(value, schema, fieldPath = "$") {
  if (schema.oneOf) {
    const matches = schema.oneOf.filter((candidate) => {
      try {
        validateSchema(value, candidate, fieldPath);
        return true;
      } catch {
        return false;
      }
    });
    if (matches.length !== 1) fail(fieldPath, "必须且只能匹配一个允许的结构");
  }
  if (schema.allOf) {
    for (const candidate of schema.allOf) validateSchema(value, candidate, fieldPath);
  }
  if (schema.if) {
    let matched = true;
    try {
      validateSchema(value, schema.if, fieldPath);
    } catch {
      matched = false;
    }
    if (matched && schema.then) validateSchema(value, schema.then, fieldPath);
    if (!matched && schema.else) validateSchema(value, schema.else, fieldPath);
  }
  if (Object.hasOwn(schema, "const") && value !== schema.const) {
    fail(fieldPath, `必须等于 ${JSON.stringify(schema.const)}`);
  }
  if (schema.enum && !schema.enum.includes(value)) {
    fail(fieldPath, `必须是 ${schema.enum.join("、")} 之一`);
  }
  if (schema.type) validateType(value, schema.type, fieldPath);

  if (schema.type === "object" || schema.properties || schema.required) {
    if (value === null || typeof value !== "object" || Array.isArray(value)) {
      fail(fieldPath, "必须是 object");
    }
    for (const key of schema.required || []) {
      if (!Object.hasOwn(value, key)) fail(`${fieldPath}.${key}`, "缺少必填字段");
    }
    const properties = schema.properties || {};
    if (schema.additionalProperties === false) {
      for (const key of Object.keys(value)) {
        if (!Object.hasOwn(properties, key)) fail(`${fieldPath}.${key}`, "未知字段");
      }
    }
    for (const [key, child] of Object.entries(properties)) {
      if (Object.hasOwn(value, key)) validateSchema(value[key], child, `${fieldPath}.${key}`);
    }
  }
  if (schema.type === "array") {
    if (schema.minItems !== undefined && value.length < schema.minItems) fail(fieldPath, `至少需要 ${schema.minItems} 项`);
    if (schema.maxItems !== undefined && value.length > schema.maxItems) fail(fieldPath, `最多允许 ${schema.maxItems} 项`);
    if (schema.items) value.forEach((item, index) => validateSchema(item, schema.items, `${fieldPath}[${index}]`));
  }
  if (schema.type === "string") {
    if (schema.minLength !== undefined && value.length < schema.minLength) fail(fieldPath, `长度不能小于 ${schema.minLength}`);
    if (schema.maxLength !== undefined && value.length > schema.maxLength) fail(fieldPath, `长度不能大于 ${schema.maxLength}`);
    if (schema.pattern && !(new RegExp(schema.pattern)).test(value)) fail(fieldPath, "格式无效");
  }
  if (schema.type === "number" || schema.type === "integer") {
    if (schema.minimum !== undefined && value < schema.minimum) fail(fieldPath, `不能小于 ${schema.minimum}`);
    if (schema.maximum !== undefined && value > schema.maximum) fail(fieldPath, `不能大于 ${schema.maximum}`);
    if (schema.exclusiveMinimum !== undefined && value <= schema.exclusiveMinimum) fail(fieldPath, `必须大于 ${schema.exclusiveMinimum}`);
    if (schema.exclusiveMaximum !== undefined && value >= schema.exclusiveMaximum) fail(fieldPath, `必须小于 ${schema.exclusiveMaximum}`);
  }
  return value;
}

const bridgeErrorCodes = new Set([
  "INVALID_REQUEST", "INVALID_ARGUMENT", "UNSUPPORTED_OPERATION", "INTERNAL_ERROR",
  "VISIO_NOT_INSTALLED", "VISIO_START_FAILED", "VISIO_BUSY", "SESSION_NOT_READY",
  "SESSION_INVALIDATED", "DOCUMENT_NOT_FOUND", "PAGE_NOT_FOUND", "SHAPE_NOT_FOUND",
  "DUPLICATE_SEMANTIC_ID", "MASTER_NOT_FOUND", "CELL_NOT_FOUND", "CONNECTOR_NOT_GLUED",
  "INVALID_COORDINATES", "INVALID_STYLE", "OUTPUT_EXISTS", "SAVE_PATH_REQUIRED",
  "SAVE_FAILED", "VALIDATION_FAILED", "EXPORT_FAILED",
]);

export function mapToolError(error, tool) {
  if (error instanceof McpValidationError) {
    return {
      error: error.code,
      message: error.message,
      retryable: false,
      details: error.details,
      tool,
    };
  }
  if (error?.code === "BRIDGE_TIMEOUT" || error?.code === "BRIDGE_EXITED") {
    return {
      error: error.code,
      message: error.message,
      retryable: error.retryable ?? false,
      details: error.details,
      tool,
    };
  }
  const bridge = error?.bridgeError;
  if (bridge && bridgeErrorCodes.has(bridge.code)) {
    return {
      error: bridge.code,
      message: bridge.message || error.message,
      retryable: bridge.retryable ?? false,
      details: bridge.details,
      tool,
    };
  }
  return {
    error: "INTERNAL_ERROR",
    message: error?.message || "未知内部错误。",
    retryable: false,
    details: bridge
      ? { ...(bridge.details || {}), bridge_code: bridge.code }
      : undefined,
    tool,
  };
}

export function mapSequenceError(error) {
  const mapped = mapToolError(error, "visio_live_draw_sequence");
  return {
    code: mapped.error,
    message: mapped.message,
    retryable: mapped.retryable,
    details: mapped.details,
  };
}

function validateType(value, type, fieldPath) {
  const valid = type === "object"
    ? value !== null && typeof value === "object" && !Array.isArray(value)
    : type === "array"
      ? Array.isArray(value)
      : type === "integer"
        ? Number.isInteger(value)
        : type === "number"
          ? typeof value === "number" && Number.isFinite(value)
          : typeof value === type;
  if (!valid) fail(fieldPath, `必须是 ${type}`);
}

function fail(fieldPath, reason) {
  throw new McpValidationError(`${fieldPath}: ${reason}`, fieldPath);
}

export function rpcResult(id, result) {
  return { jsonrpc: "2.0", id, result };
}

export function rpcError(id, code, message, data) {
  return {
    jsonrpc: "2.0",
    id: id ?? null,
    error: {
      code,
      message,
      ...(data === undefined ? {} : { data }),
    },
  };
}

export function toolResult(value, { imageData, isError = false } = {}) {
  const content = [{ type: "text", text: JSON.stringify(value, null, 2) }];
  if (imageData) {
    content.push({ type: "image", data: imageData, mimeType: "image/png" });
  }
  return { content, structuredContent: value, isError };
}
