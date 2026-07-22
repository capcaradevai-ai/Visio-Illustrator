import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { createInterface } from "node:readline";

export class BridgeClient {
  constructor(command, args, { cwd, readyTimeoutMs = 10000 } = {}) {
    this.command = command;
    this.args = args;
    this.cwd = cwd;
    this.readyTimeoutMs = readyTimeoutMs;
    this.nextId = 1;
    this.pending = new Map();
    this.child = null;
    this.lines = null;
    this.ready = null;
    this.resolveReady = null;
    this.rejectReady = null;
  }

  async start() {
    if (this.child) throw new Error("BRIDGE_ALREADY_STARTED");

    this.child = spawn(this.command, this.args, {
      cwd: this.cwd,
      stdio: ["pipe", "pipe", "pipe"],
      windowsHide: true,
    });
    this.child.stderr.on("data", (chunk) => process.stderr.write(chunk));
    this.lines = createInterface({ input: this.child.stdout, crlfDelay: Infinity });

    const ready = new Promise((resolve, reject) => {
      const timer = setTimeout(
        () => reject(new Error("BRIDGE_READY_TIMEOUT")),
        this.readyTimeoutMs,
      );
      this.resolveReady = (value) => {
        clearTimeout(timer);
        resolve(value);
      };
      this.rejectReady = (error) => {
        clearTimeout(timer);
        reject(error);
      };
    });

    this.lines.on("line", (line) => this.#handleLine(line));
    this.child.once("error", (error) => {
      this.#failAll(new Error(`BRIDGE_START_FAILED: ${error.message}`));
    });
    this.child.once("exit", (code) => {
      this.#failAll(new Error(`BRIDGE_EXITED: ${code}`));
    });

    this.ready = await ready;
    return this.ready;
  }

  call(command, params, timeoutMs = 30000) {
    if (!this.child || !this.ready) return Promise.reject(new Error("BRIDGE_NOT_READY"));
    const id = this.nextId++;

    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`BRIDGE_TIMEOUT: ${command}`));
      }, timeoutMs);
      this.pending.set(id, {
        resolve: (value) => {
          clearTimeout(timer);
          resolve(value);
        },
        reject: (error) => {
          clearTimeout(timer);
          reject(error);
        },
      });
      this.child.stdin.write(`${JSON.stringify({ id, command, params })}\n`);
    });
  }

  async stop() {
    if (!this.child) return;
    const child = this.child;
    const exited = child.exitCode === null
      ? new Promise((resolve) => child.once("exit", resolve))
      : Promise.resolve();
    let shutdownTimer;
    child.stdin.end();
    await Promise.race([
      exited,
      new Promise((resolve) => {
        shutdownTimer = setTimeout(resolve, 2000);
      }),
    ]);
    clearTimeout(shutdownTimer);
    if (child.exitCode === null) child.kill();
    this.lines?.close();
    this.#failAll(new Error("BRIDGE_STOPPED"));
    this.child = null;
    this.lines = null;
    this.ready = null;
    this.resolveReady = null;
    this.rejectReady = null;
  }

  #handleLine(line) {
    let message;
    try {
      message = JSON.parse(line);
    } catch {
      this.#failAll(new Error("BRIDGE_PROTOCOL_ERROR"));
      return;
    }

    if (message.type === "ready" && !this.ready) {
      this.resolveReady?.(message);
      return;
    }
    const pending = this.pending.get(message.id);
    if (!pending) return;
    this.pending.delete(message.id);
    if (message.ok) {
      pending.resolve(message.result);
      return;
    }
    pending.reject(Object.assign(
      new Error(message.error?.message || "BRIDGE_ERROR"),
      { bridgeError: message.error },
    ));
  }

  #failAll(error) {
    this.rejectReady?.(error);
    for (const pending of this.pending.values()) pending.reject(error);
    this.pending.clear();
  }
}

export function resolveBridgeExecutable(pluginRoot) {
  const configured = process.env.VISIO_BRIDGE_PATH?.trim();
  const candidate = configured || path.join(pluginRoot, "bin", "win-x64", "VisioBridge.exe");
  if (!path.isAbsolute(candidate) || !existsSync(candidate)) {
    throw new Error(`BRIDGE_NOT_FOUND: ${candidate}`);
  }
  return candidate;
}
