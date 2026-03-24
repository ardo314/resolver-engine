import { connect } from "nats";
import { EntityRepository } from "./entity-repository.js";
import { EntityService } from "./entity-service.js";

async function main(): Promise<void> {
  const nc = await connect();
  console.log("Connected to NATS.");

  const repo = new EntityRepository();
  const service = new EntityService(nc, repo);
  await service.start();

  console.log("Engine.Backend running – press Ctrl+C to stop.");

  // Graceful shutdown
  const shutdown = async () => {
    console.log("\nShutting down...");
    await service.stop();
    await nc.drain();
    process.exit(0);
  };

  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);

  // Keep running until signal
  await nc.closed();
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
