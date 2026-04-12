FROM node:25-slim AS build

WORKDIR /app

# Copy root workspace files
COPY package.json tsconfig.json tsconfig.packages.json ./

# Copy all packages the nova worker depends on
COPY engine/core/package.json engine/core/
COPY engine/worker/package.json engine/worker/
COPY modules/core/package.json modules/core/
COPY modules/nova/package.json modules/nova/
COPY workers/nova/package.json workers/nova/

RUN npm install

# Copy source and tsconfigs
COPY engine/core/tsconfig.json engine/core/
COPY engine/core/src/ engine/core/src/
COPY engine/worker/tsconfig.json engine/worker/
COPY engine/worker/src/ engine/worker/src/
COPY modules/core/tsconfig.json modules/core/
COPY modules/core/src/ modules/core/src/
COPY modules/nova/tsconfig.json modules/nova/
COPY modules/nova/src/ modules/nova/src/
COPY workers/nova/tsconfig.json workers/nova/
COPY workers/nova/src/ workers/nova/src/

RUN npx tsc --build workers/nova

# --- production image ---
FROM node:25-slim

WORKDIR /app

COPY package.json ./
COPY engine/core/package.json engine/core/
COPY engine/worker/package.json engine/worker/
COPY modules/core/package.json modules/core/
COPY modules/nova/package.json modules/nova/
COPY workers/nova/package.json workers/nova/

RUN npm install --workspace=@engine/core --workspace=@engine/worker --workspace=@ardo314/core --workspace=@ardo314/nova --workspace=@ardo314/nova-workers --omit=dev

COPY --from=build /app/engine/core/dist/ engine/core/dist/
COPY --from=build /app/engine/worker/dist/ engine/worker/dist/
COPY --from=build /app/modules/core/dist/ modules/core/dist/
COPY --from=build /app/modules/nova/dist/ modules/nova/dist/
COPY --from=build /app/workers/nova/dist/ workers/nova/dist/

USER node

ENV NATS_URL=nats://localhost:4222

CMD ["node", "workers/nova/dist/index.js"]
