import path from "node:path";
import { fileURLToPath } from "node:url";
import { BridgeClient } from "../../plugins/visio-scientific-illustrator-test/scripts/bridge-client.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const client = new BridgeClient(process.execPath, [path.join(here, "fake-bridge.mjs")]);
await client.start();
await client.stop();
