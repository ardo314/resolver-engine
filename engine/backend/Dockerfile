FROM node:25-slim AS build

WORKDIR /app

# Copy root workspace files
COPY package.json tsconfig.json tsconfig.packages.json ./

# Copy only the packages backend depends on
COPY engine/core/package.json engine/core/
COPY engine/backend/package.json engine/backend/

RUN npm install --workspace=@engine/core --workspace=@engine/backend

# Copy source for core and backend
COPY engine/core/tsconfig.json engine/core/
COPY engine/core/src/ engine/core/src/
COPY engine/backend/tsconfig.json engine/backend/
COPY engine/backend/src/ engine/backend/src/

RUN npx tsc --build engine/backend

# --- production image ---
FROM node:25-slim

WORKDIR /app

COPY package.json ./
COPY engine/core/package.json engine/core/
COPY engine/backend/package.json engine/backend/

RUN npm install --workspace=@engine/core --workspace=@engine/backend --omit=dev

COPY --from=build /app/engine/core/dist/ engine/core/dist/
COPY --from=build /app/engine/backend/dist/ engine/backend/dist/

USER node

ENV NATS_URL=nats://localhost:4222

CMD ["node", "engine/backend/dist/index.js"]
