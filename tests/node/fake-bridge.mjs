import { createInterface } from "node:readline";

if (!process.argv.includes("--skip-ready")) {
  process.stdout.write(`${JSON.stringify({
    type: "ready",
    bridgeVersion: "test",
    processId: process.pid,
    architecture: "X64",
  })}\n`);
}

const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
lines.on("line", (line) => {
  const request = JSON.parse(line);
  if (request.command === "hang") return;
  if (request.command === "exit") {
    process.exit(7);
  }
  if (request.command === "invalid") {
    process.stdout.write("{invalid-json}\n");
    return;
  }
  process.stdout.write(`${JSON.stringify({
    id: request.id,
    ok: true,
    result: { command: request.command },
  })}\n`);
});
