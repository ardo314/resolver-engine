FROM node:25-slim AS build

WORKDIR /app

# Copy root workspace files
COPY package.json tsconfig.json tsconfig.packages.json ./

# Copy all packages the in-memory worker depends on
COPY engine/core/package.json engine/core/
COPY engine/worker/package.json engine/worker/
COPY modules/core/package.json modules/core/
COPY modules/in-memory/package.json modules/in-memory/
COPY workers/in-memory/package.json workers/in-memory/

RUN npm install

# Copy source and tsconfigs
COPY engine/core/tsconfig.json engine/core/
COPY engine/core/src/ engine/core/src/
COPY engine/worker/tsconfig.json engine/worker/
COPY engine/worker/src/ engine/worker/src/
COPY modules/core/tsconfig.json modules/core/
COPY modules/core/src/ modules/core/src/
COPY modules/in-memory/tsconfig.json modules/in-memory/
COPY modules/in-memory/src/ modules/in-memory/src/
COPY workers/in-memory/tsconfig.json workers/in-memory/
COPY workers/in-memory/src/ workers/in-memory/src/

RUN npx tsc --build workers/in-memory

# --- production image ---
FROM node:25-slim

WORKDIR /app

COPY package.json ./
COPY engine/core/package.json engine/core/
COPY engine/worker/package.json engine/worker/
COPY modules/core/package.json modules/core/
COPY modules/in-memory/package.json modules/in-memory/
COPY workers/in-memory/package.json workers/in-memory/

RUN npm install --workspace=@engine/core --workspace=@engine/worker --workspace=@ardo314/core --workspace=@ardo314/in-memory --workspace=@ardo314/in-memory-workers --omit=dev

COPY --from=build /app/engine/core/dist/ engine/core/dist/
COPY --from=build /app/engine/worker/dist/ engine/worker/dist/
COPY --from=build /app/modules/core/dist/ modules/core/dist/
COPY --from=build /app/modules/in-memory/dist/ modules/in-memory/dist/
COPY --from=build /app/workers/in-memory/dist/ workers/in-memory/dist/

USER node

ENV NATS_URL=nats://localhost:4222

CMD ["node", "workers/in-memory/dist/index.js"]
