#!/bin/sh
# lgym-logpanel entrypoint.
# Generates a self-signed TLS cert (if missing) and the basic-auth htpasswd
# file from env, then hands control to supervisord which runs ES + Kibana + nginx.

set -e

TLS_CRT=/etc/nginx/tls.crt
TLS_KEY=/etc/nginx/tls.key
HTPASSWD=/etc/nginx/.htpasswd

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
chmod 640 "$HTPASSWD"

# --- Hand off to supervisord (PID 1) ---------------------------------------
exec supervisord -c /etc/supervisor/conf.d/supervisord.conf
