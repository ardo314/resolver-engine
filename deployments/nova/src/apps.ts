import type { App } from "@wandelbots/nova-api/v2";

export function backendApp(
  image: string,
  cellName: string,
  natsUrl: string,
  natsUser?: string,
  natsPass?: string,
): App {
  const env = [
    { name: "NATS_URL", value: natsUrl },
    { name: "BASE_PATH", value: `/${cellName}/component-engine-backend` },
  ];
  if (natsUser) env.push({ name: "NATS_USER", value: natsUser });
  if (natsPass) env.push({ name: "NATS_PASS", value: natsPass });

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
  cellName: string,
  natsUrl: string,
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
