import test from "node:test";
import assert from "node:assert/strict";
import {
  SerialQueue,
  validateObject,
} from "../../plugins/visio-scientific-illustrator-test/scripts/mcp-common.mjs";

test("SerialQueue 按提交顺序完成状态修改", async () => {
  const queue = new SerialQueue();
  const order = [];
  const first = queue.run(async () => {
    await new Promise((resolve) => setTimeout(resolve, 20));
    order.push("first");
  });
  const second = queue.run(async () => order.push("second"));

  await Promise.all([first, second]);

  assert.deepEqual(order, ["first", "second"]);
});

test("validateObject 拒绝未知字段", () => {
  assert.throws(
    () => validateObject(
      { mode: "new", extra: true },
      { required: ["mode"], allowed: ["mode"] },
    ),
    /未知字段: extra/,
  );
});
