#!/bin/sh
set -e

HTML_DIR="/usr/share/nginx/html"

NATS_URL="${NATS_URL:-ws://localhost:9222}"
BASE_PATH="${BASE_PATH:-/}"

# Ensure BASE_PATH starts and ends with /
case "$BASE_PATH" in
  /*) ;;
  *)  BASE_PATH="/$BASE_PATH" ;;
esac
case "$BASE_PATH" in
  */) ;;
  *)  BASE_PATH="$BASE_PATH/" ;;
esac

# --- Generate runtime configuration ---
cat > "$HTML_DIR/env.js" <<EOF
window.__ENV__ = {
  NATS_URL: "${NATS_URL}",
  BASE_PATH: "${BASE_PATH}",
};
EOF

# --- Generate nginx config from template ---
NGINX_CONF="/etc/nginx/conf.d/default.conf"
sed "s|__BASE_PATH__|${BASE_PATH}|g" /etc/nginx/conf.d/default.conf.template > "$NGINX_CONF"

exec nginx -g 'daemon off;'
