FROM node:25-slim AS build

WORKDIR /app

# Copy root workspace files
COPY package.json tsconfig.json tsconfig.packages.json ./

# Copy only the packages editor depends on
COPY engine/core/package.json engine/core/
COPY engine/client/package.json engine/client/
COPY engine/editor/package.json engine/editor/

RUN npm install

# Copy source for core, client and editor
COPY engine/core/tsconfig.json engine/core/
COPY engine/core/src/ engine/core/src/
COPY engine/client/tsconfig.json engine/client/
COPY engine/client/src/ engine/client/src/
COPY engine/editor/tsconfig.json engine/editor/
COPY engine/editor/tsconfig.app.json engine/editor/
COPY engine/editor/tsconfig.node.json engine/editor/
COPY engine/editor/vite.config.ts engine/editor/
COPY engine/editor/index.html engine/editor/
COPY engine/editor/public/ engine/editor/public/
COPY engine/editor/src/ engine/editor/src/
COPY engine/editor/eslint.config.js engine/editor/

RUN npm run build --workspace=@engine/core --workspace=@engine/client
RUN npm run build --workspace=@engine/editor

# --- production image ---
FROM nginx:alpine

# Remove the default site so our config is the only one.
RUN rm /etc/nginx/conf.d/default.conf

COPY engine/editor/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/engine/editor/dist/ /usr/share/nginx/html/
COPY engine/editor/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
