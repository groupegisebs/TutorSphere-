#!/usr/bin/env bash
# Assemble le fichier .env de production pour Docker Compose (convention ASP.NET Core Section__Key).

set -euo pipefail

OUT="${1:-/tmp/tutorsphere.app.env}"
umask 077
: > "$OUT"

: "${CONNECTION_STRING:?CONNECTION_STRING requis}"
: "${JWT_KEY:?JWT_KEY requis}"
: "${PAYGATEWAY_BASE_URL:?PAYGATEWAY_BASE_URL requis}"
: "${PAYGATEWAY_API_KEY:?PAYGATEWAY_API_KEY requis}"

{
  printf 'ASPNETCORE_ENVIRONMENT=Production\n'
  printf 'CONNECTIONSTRINGS__DEFAULTCONNECTION=%s\n' "${CONNECTION_STRING}"
  printf 'JWT__KEY=%s\n' "${JWT_KEY}"
  printf 'JWT__ISSUER=%s\n' "${JWT__ISSUER:-TutorSphere}"
  printf 'JWT__AUDIENCE=%s\n' "${JWT__AUDIENCE:-TutorSphere}"
  printf 'PAYGATEWAY__BASEURL=%s\n' "${PAYGATEWAY_BASE_URL}"
  printf 'PAYGATEWAY__APPCODE=%s\n' "${PAYGATEWAY_APP_CODE:-TUTORSPHERE}"
  printf 'PAYGATEWAY__APIKEY=%s\n' "${PAYGATEWAY_API_KEY}"
  # true = Stripe Test (X-Stripe-Env: DEV) ; false = Stripe Live (pas de header)
  printf 'PAYGATEWAY__USESANDBOX=%s\n' "${PAYGATEWAY_USE_SANDBOX:-false}"
  printf 'EMAIL__BASEURL=%s\n' "${EMAIL_BASE_URL:-https://gisemailsender.gisebs.com}"
  printf 'EMAIL__APIKEY=%s\n' "${EMAIL_API_KEY:-}"
  printf 'EMAIL__CLIENTCODE=%s\n' "${EMAIL_CLIENT_CODE:-TUTORSPHERE}"
  printf 'APIBASEURL=%s\n' "${API_BASE_URL:-https://api.tutorsphere.gisebs.com}"
  printf 'WEBBASEURL=%s\n' "${WEB_BASE_URL:-https://tutorsphere.gisebs.com}"
  printf 'INTERNALAPIBASEURL=http://127.0.0.1:%s\n' "${API_PORT:-55099}"
  printf 'SEED__RESETKNOWNADMINPASSWORDS=%s\n' "${SEED_RESET_KNOWN_ADMIN_PASSWORDS:-true}"
  printf 'API_PORT=%s\n' "${API_PORT:-55099}"
  printf 'WEB_PORT=%s\n' "${WEB_PORT:-55010}"
} >> "$OUT"

chmod 600 "$OUT"
echo "Fichier .env généré : ${OUT}"
