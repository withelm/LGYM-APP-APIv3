#!/usr/bin/env bash
#
# Apply the Google OIDC -> Elastic "viewer" role mapping for the logging stack.
#
# This is an operator-applied step (by design): group claims are operator-managed
# and must NOT be baked into git. Run this AFTER the stack is up and the
# `google_oidc` realm is live, and BEFORE browser QA.
#
# Usage:
#   LOGGING_ES_PASSWORD=... bash deploy/es/apply-role-mapping.sh
#
# Optional overrides:
#   LOGGING_ES_URL                 (default https://localhost:9200)
#   LOGGING_ES_USER                (default elastic)
#   LOGGING_GOOGLE_OIDC_VIEWER_GROUP (default kibana-viewers@lgym.ovh)

set -euo pipefail

LOGGING_ES_URL="${LOGGING_ES_URL:-https://localhost:9200}"
LOGGING_ES_USER="${LOGGING_ES_USER:-elastic}"
LOGGING_GOOGLE_OIDC_VIEWER_GROUP="${LOGGING_GOOGLE_OIDC_VIEWER_GROUP:-kibana-viewers@lgym.ovh}"

if [ -z "${LOGGING_ES_PASSWORD:-}" ]; then
  echo "ERROR: LOGGING_ES_PASSWORD is required (the operator-managed ES superuser password)." >&2
  echo "Usage: LOGGING_ES_PASSWORD=... bash deploy/es/apply-role-mapping.sh" >&2
  exit 1
fi

MAPPING_NAME="google_oidc_kibana_viewers"

echo "Applying role mapping '${MAPPING_NAME}' to ${LOGGING_ES_URL} (realm google_oidc, group ${LOGGING_GOOGLE_OIDC_VIEWER_GROUP})..."

curl -k -sS -X PUT \
  -u "${LOGGING_ES_USER}:${LOGGING_ES_PASSWORD}" \
  -H "Content-Type: application/json" \
  "${LOGGING_ES_URL}/_security/role_mapping/${MAPPING_NAME}" \
  -d "{
  \"enabled\": true,
  \"roles\": [\"viewer\"],
  \"rules\": {
    \"all\": [
      { \"field\": { \"realm.name\": \"google_oidc\" } },
      { \"field\": { \"groups\": \"${LOGGING_GOOGLE_OIDC_VIEWER_GROUP}\" } }
    ]
  },
  \"metadata\": {
    \"owner\": \"lgym\",
    \"purpose\": \"Grant Kibana read-only viewer access only to approved Google Group members\"
  }
}"

echo
echo "Role mapping '${MAPPING_NAME}' applied: realm google_oidc + group ${LOGGING_GOOGLE_OIDC_VIEWER_GROUP} -> Elastic role 'viewer'."
