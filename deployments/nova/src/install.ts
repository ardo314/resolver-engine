import {
  type App,
  ApplicationApi,
  Configuration,
} from "@wandelbots/nova-api/v2";
import { isAxiosError } from "axios";
import { backendApp, editorApp } from "./apps.js";

const novaApi = process.env.NOVA_API;
const cellName = process.env.CELL_NAME;
const natsBroker = process.env.NATS_BROKER;

if (!novaApi)
  throw new Error("NOVA_API is not set — must run inside a NOVA cell app");
if (!cellName)
  throw new Error("CELL_NAME is not set — must run inside a NOVA cell app");
if (!natsBroker)
  throw new Error("NATS_BROKER is not set — must run inside a NOVA cell app");

const NOVA_API_URL = novaApi;
const CELL = cellName;
const NATS = natsBroker;

const backendImage = process.env.BACKEND_IMAGE;
const editorImage = process.env.EDITOR_IMAGE;

if (!backendImage) throw new Error("BACKEND_IMAGE is not set");
if (!editorImage) throw new Error("EDITOR_IMAGE is not set");

const config = new Configuration({ basePath: `${NOVA_API_URL}/api/v2` });
const api = new ApplicationApi(config);

async function installApp(app: App) {
  console.log(`Installing app '${app.name}' into cell '${CELL}'...`);
  try {
    await api.addApp(CELL, app);
    console.log(`  -> '${app.name}' installed`);
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 409) {
      console.log(`  -> '${app.name}' already exists, skipping`);
    } else {
      throw err;
    }
  }
}

await installApp(backendApp(backendImage, NATS, CELL));

await installApp(editorApp(editorImage, "/nats", CELL));

console.log("\nAll apps installed. component-engine-nova done.");

// Keep the container alive so NOVA does not restart it.
await new Promise(() => {});
