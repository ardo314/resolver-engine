#!/bin/sh
set -e

HTML_DIR="/usr/share/nginx/html"

NATS_URL="${NATS_URL:-ws://localhost:9222}"

cat > "$HTML_DIR/env.js" <<EOF
window.__ENV__ = {
  NATS_URL: "${NATS_URL}",
};
EOF

exec nginx -g 'daemon off;'
