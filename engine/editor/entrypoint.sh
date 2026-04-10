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

if [ "$BASE_PATH" = "/" ]; then
  BASE_PATH_NO_SLASH="/__no_base_path_redirect__"
  ROOT_LOCATION_DIRECTIVE="try_files /index.html =404;"
else
  BASE_PATH_NO_SLASH="${BASE_PATH%/}"
  ROOT_LOCATION_DIRECTIVE="return 302 ${BASE_PATH};"
fi

# --- Generate runtime configuration ---
cat > "$HTML_DIR/env.js" <<EOF
window.__ENV__ = {
  NATS_URL: "${NATS_URL}",
  BASE_PATH: "${BASE_PATH}",
};
EOF

# --- Generate nginx config from template ---
NGINX_CONF="/etc/nginx/conf.d/default.conf"
sed -e "s|__ROOT_LOCATION_DIRECTIVE__|${ROOT_LOCATION_DIRECTIVE}|g" -e "s|__BASE_PATH_NO_SLASH__|${BASE_PATH_NO_SLASH}|g" -e "s|__BASE_PATH__|${BASE_PATH}|g" /etc/nginx/conf.d/default.conf.template > "$NGINX_CONF"

exec nginx -g 'daemon off;'
