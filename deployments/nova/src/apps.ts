import type { App } from "@wandelbots/nova-api/v2";

export function backendApp(
  image: string,
  natsBroker: string,
  cellName: string,
): App {
  const url = new URL(natsBroker);
  const user = url.username;
  const pass = url.password;
  url.username = "";
  url.password = "";
  const natsUrl = url.toString();

  const env = [
    { name: "NATS_URL", value: natsUrl },
    { name: "BASE_PATH", value: `/${cellName}/component-engine-backend` },
  ];
  if (user) env.push({ name: "NATS_USER", value: user });
  if (pass) env.push({ name: "NATS_PASS", value: pass });

  return {
    name: "component-engine-backend",
    app_icon: "favicon.ico",
    container_image: { image },
    port: 8080,
    environment: env,
  };
}

export function editorApp(
  image: string,
  natsUrl: string,
  cellName: string,
): App {
  return {
    name: "component-engine-editor",
    app_icon: "favicon.ico",
    container_image: { image },
    port: 8080,
    environment: [
      { name: "NATS_URL", value: natsUrl },
      { name: "BASE_PATH", value: `/${cellName}/component-engine-editor` },
    ],
  };
}
