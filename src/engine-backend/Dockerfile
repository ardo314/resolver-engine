FROM node:22-slim AS build
WORKDIR /app
COPY package.json package-lock.json* ./
COPY src/engine-core/package.json src/engine-core/
COPY src/engine-client/package.json src/engine-client/
COPY src/engine-worker/package.json src/engine-worker/
COPY src/engine-backend/package.json src/engine-backend/
COPY src/engine-worker-runtime/package.json src/engine-worker-runtime/
COPY modules/in-memory/package.json modules/in-memory/
COPY modules/in-memory-workers/package.json modules/in-memory-workers/
RUN npm ci --ignore-scripts
COPY tsconfig.json tsconfig.packages.json ./
COPY src/ src/
COPY modules/ modules/
RUN npx tsc --build

FROM node:22-slim
WORKDIR /app
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/src/engine-core/dist ./src/engine-core/dist
COPY --from=build /app/src/engine-core/package.json ./src/engine-core/
COPY --from=build /app/src/engine-backend/dist ./src/engine-backend/dist
COPY --from=build /app/src/engine-backend/package.json ./src/engine-backend/
COPY --from=build /app/package.json ./
CMD ["node", "src/engine-backend/dist/program.js"]
