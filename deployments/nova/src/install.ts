import {
  type App,
  ApplicationApi,
  Configuration,
} from "@wandelbots/nova-api/v2";
import assert from "node:assert";
import { isAxiosError } from "axios";
import { backendApp, editorApp } from "./apps.js";

const novaApi = process.env.NOVA_API;
const cellName = process.env.CELL_NAME;
const natsBroker = process.env.NATS_BROKER;
const backendImage = process.env.BACKEND_IMAGE;
const editorImage = process.env.EDITOR_IMAGE;

assert(novaApi, "NOVA_API is not set");
assert(cellName, "CELL_NAME is not set");
assert(natsBroker, "NATS_BROKER is not set");
assert(backendImage, "BACKEND_IMAGE is not set");
assert(editorImage, "EDITOR_IMAGE is not set");

const config = new Configuration({ basePath: `http://${novaApi}/api/v2` });
const api = new ApplicationApi(config);
const natsBrokerUrl = new URL(natsBroker);
const natsUser = natsBrokerUrl.username;
const natsPass = natsBrokerUrl.password;
natsBrokerUrl.username = "";
natsBrokerUrl.password = "";
const natsUrl = natsBrokerUrl.toString();

async function installApp(cell: string, app: App) {
  console.log(`Installing app '${app.name}' into cell '${cell}'...`);
  try {
    await api.addApp(cell, app);
    console.log(`  -> '${app.name}' installed`);
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 409) {
      console.log(`  -> '${app.name}' already exists, skipping`);
    } else {
      throw err;
    }
  }
}

await installApp(
  cellName,
  backendApp(backendImage, cellName, natsUrl, natsUser, natsPass),
);

await installApp(cellName, editorApp(editorImage, cellName, "/api/nats"));

console.log("\nAll apps installed. component-engine-nova done.");

while (true) {
  await new Promise((resolve) => setTimeout(resolve, 1000));
}
