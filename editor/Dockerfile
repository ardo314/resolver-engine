FROM node:25-slim AS build

WORKDIR /app

# Copy root workspace files
COPY package.json tsconfig.json tsconfig.packages.json ./

# Copy only the packages editor depends on
COPY clients/js/package.json clients/js/
COPY editor/package.json editor/

RUN npm install

# Copy source for client and editor
COPY clients/js/tsconfig.json clients/js/
COPY clients/js/src/ clients/js/src/
COPY editor/tsconfig.json editor/
COPY editor/tsconfig.app.json editor/
COPY editor/tsconfig.node.json editor/
COPY editor/vite.config.ts editor/
COPY editor/index.html editor/
COPY editor/public/ editor/public/
COPY editor/src/ editor/src/
COPY editor/eslint.config.js editor/

RUN npm run build --workspace=@engine/client
RUN npm run build --workspace=@engine/editor

# --- production image ---
FROM nginx:alpine

# Remove the default site so our config is the only one.
RUN rm /etc/nginx/conf.d/default.conf

COPY editor/nginx.conf /etc/nginx/conf.d/default.conf.template
COPY --from=build /app/editor/dist/ /usr/share/nginx/html/
COPY editor/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
