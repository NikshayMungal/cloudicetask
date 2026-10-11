#!/bin/sh
# Writes /config.js from environment variables when the container starts, so the same image can be
# pointed at any backend URL without rebuilding.
set -e
cat > /usr/share/nginx/html/config.js <<EOT
window.__CONFIG__ = { API_BASE_URL: "${API_BASE_URL:-}" };
EOT
