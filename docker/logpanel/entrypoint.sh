#!/bin/sh
# lgym-logpanel entrypoint.
# Generates a self-signed TLS cert (if missing) and the basic-auth htpasswd
# file from env, then hands control to supervisord which runs ES + Kibana + nginx.

set -e

TLS_CRT=/etc/nginx/tls.crt
TLS_KEY=/etc/nginx/tls.key
HTPASSWD=/etc/nginx/.htpasswd
KIBANA_CONFIG=/opt/kibana/config/kibana.yml
KIBANA_CONFIG_HOME=/opt/kibana/.config
KIBANA_CACHE_HOME=/opt/kibana/.cache

# --- TLS: generate a throwaway self-signed cert if none is mounted ----------
if [ ! -f "$TLS_CRT" ] || [ ! -f "$TLS_KEY" ]; then
    echo "[entrypoint] No TLS cert found at $TLS_CRT / $TLS_KEY - generating self-signed cert."
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout "$TLS_KEY" -out "$TLS_CRT" \
        -subj "/CN=localhost"
fi

# --- Basic-auth credentials from env (with insecure defaults) ---------------
LOGPANEL_USER="${LOGPANEL_USER:-admin}"
LOGPANEL_PASSWORD="${LOGPANEL_PASSWORD:-admin12345}"

if [ "$LOGPANEL_USER" = "admin" ] && [ "$LOGPANEL_PASSWORD" = "admin12345" ]; then
    echo "==================================================================="
    echo " WARNING: Using INSECURE default credentials (admin / admin12345)."
    echo "          Override LOGPANEL_USER and LOGPANEL_PASSWORD in production!"
    echo "==================================================================="
fi

htpasswd -bc "$HTPASSWD" "$LOGPANEL_USER" "$LOGPANEL_PASSWORD"
chown root:www-data "$HTPASSWD"
chmod 640 "$HTPASSWD"

# --- Kibana writable HOME/XDG paths for Puppeteer/reporting -----------------
mkdir -p "$KIBANA_CONFIG_HOME" "$KIBANA_CACHE_HOME"
chown -R kibana:kibana "$KIBANA_CONFIG_HOME" "$KIBANA_CACHE_HOME"

# --- Kibana config cleanup: keep ES security-off, remove Kibana invalid key --
if grep -q '^xpack.security.enabled:' "$KIBANA_CONFIG"; then
    sed -i '/^xpack.security.enabled:/d' "$KIBANA_CONFIG"
fi

# --- Kibana public base URL: runtime-only and optional ----------------------
LOGPANEL_PUBLIC_BASE_URL="${LOGPANEL_PUBLIC_BASE_URL:-}"

if grep -q '^server.publicBaseUrl:' "$KIBANA_CONFIG"; then
    sed -i '/^server.publicBaseUrl:/d' "$KIBANA_CONFIG"
fi

if [ -n "$LOGPANEL_PUBLIC_BASE_URL" ]; then
    printf '\nserver.publicBaseUrl: "%s"\n' "$LOGPANEL_PUBLIC_BASE_URL" >> "$KIBANA_CONFIG"
fi

# --- Hand off to supervisord (PID 1) ---------------------------------------
exec supervisord -c /etc/supervisor/conf.d/supervisord.conf
