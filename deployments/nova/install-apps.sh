#!/usr/bin/env bash
#
# component-engine-nova installer script — runs inside a NOVA cell app container.
# Installs component-engine services as apps into the cell via the NOVA API.
#
# NOVA auto-injects these environment variables:
#   NOVA_API    — API endpoint reachable from within the container
#   NATS_BROKER — NATS broker endpoint reachable from within the container
#   CELL_NAME   — Name of the hosting cell
#   BASE_PATH   — App root path
#
set -euo pipefail

: "${NOVA_API:?NOVA_API is not set — must run inside a NOVA cell app}"
: "${CELL_NAME:?CELL_NAME is not set — must run inside a NOVA cell app}"
: "${NATS_BROKER:?NATS_BROKER is not set — must run inside a NOVA cell app}"

API_URL="${NOVA_API}/api/v2"

# ---------------------------------------------------------------------------
# Helper: install an app into the cell.
# Usage: install_app '<json payload>'
# ---------------------------------------------------------------------------
install_app() {
  local payload="$1"
  local app_name
  app_name=$(echo "${payload}" | jq -r '.name')

  echo "Installing app '${app_name}' into cell '${CELL_NAME}'..."

  local http_code body response
  response=$(curl -s -w "\n%{http_code}" -X POST \
    "${API_URL}/cells/${CELL_NAME}/apps" \
    -H "Content-Type: application/json" \
    -d "${payload}")

  http_code=$(echo "${response}" | tail -1)
  body=$(echo "${response}" | sed '$d')

  if [[ "${http_code}" =~ ^2 ]]; then
    echo "  -> '${app_name}' installed (HTTP ${http_code})"
  elif [[ "${http_code}" == "409" ]]; then
    echo "  -> '${app_name}' already exists, skipping"
  else
    echo "  -> FAILED to install '${app_name}' (HTTP ${http_code}): ${body}" >&2
    return 1
  fi
}

# ---------------------------------------------------------------------------
# App definitions
# ---------------------------------------------------------------------------

# Backend — entity structure management over NATS
VERSION="${VERSION:-latest}"
BACKEND_IMAGE="${BACKEND_IMAGE:-ghcr.io/ardo314/component-engine-backend:${VERSION}}"

install_app "$(cat <<EOF
{
  "name": "component-engine-backend",
  "app_icon": "favicon.ico",
  "container_image": {
    "image": "${BACKEND_IMAGE}"
  },
  "port": 8080,
  "environment": [
    { "name": "NATS_URL", "value": "${NATS_BROKER}" },
    { "name": "BASE_PATH", "value": "/${CELL_NAME}/component-engine-backend" }
  ]
}
EOF
)"

# Editor — Vite + React frontend served via nginx
EDITOR_IMAGE="${EDITOR_IMAGE:-ghcr.io/ardo314/component-engine-editor:${VERSION}}"

NATS_WS_URL="/nats"

install_app "$(cat <<EOF
{
  "name": "component-engine-editor",
  "app_icon": "favicon.ico",
  "container_image": {
    "image": "${EDITOR_IMAGE}"
  },
  "port": 8080,
  "environment": [
    { "name": "NATS_URL", "value": "${NATS_WS_URL}" },
    { "name": "BASE_PATH", "value": "/${CELL_NAME}/component-engine-editor" }
  ]
}
EOF
)"

# ---------------------------------------------------------------------------
# Add more apps here, e.g. workers
# ---------------------------------------------------------------------------

echo ""
echo "All apps installed. component-engine-nova done."

# Keep the container alive so NOVA does not restart it.
exec tail -f /dev/null
