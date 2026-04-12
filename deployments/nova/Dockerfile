FROM node:25-slim AS build

WORKDIR /app

# Copy root workspace files
COPY package.json tsconfig.json tsconfig.packages.json ./

# Copy only the nova-deploy package
COPY deployments/nova/package.json deployments/nova/
COPY deployments/nova/tsconfig.json deployments/nova/

RUN npm install

COPY deployments/nova/src/ deployments/nova/src/

RUN npx tsc --build deployments/nova

# --- production image ---
FROM node:25-slim

ARG BACKEND_IMAGE
ARG EDITOR_IMAGE
ARG WORKER_IMAGE_0
ARG WORKER_IMAGE_1
ENV BACKEND_IMAGE=${BACKEND_IMAGE}
ENV EDITOR_IMAGE=${EDITOR_IMAGE}
ENV WORKER_IMAGE_0=${WORKER_IMAGE_0}
ENV WORKER_IMAGE_1=${WORKER_IMAGE_1}

WORKDIR /app

COPY package.json ./
COPY deployments/nova/package.json deployments/nova/

RUN npm install --workspace=@engine/nova-deploy --omit=dev

COPY --from=build /app/deployments/nova/dist/ deployments/nova/dist/

USER node

CMD ["node", "deployments/nova/dist/install.js"]


