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
