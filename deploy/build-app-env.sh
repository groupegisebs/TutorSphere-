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

# docker-compose (v1) interpole $VAR dans .env. Un $ littéral dans un secret
# est vu comme une variable vide → PostgreSQL / JWT cassés, API unhealthy.
# $$ est restauré en $ à la lecture Compose.
escape_compose_env() {
  printf '%s' "$1" | sed 's/\$/$$/g'
}

put() {
  printf '%s=%s\n' "$1" "$(escape_compose_env "$2")"
}

{
  printf 'ASPNETCORE_ENVIRONMENT=Production\n'
  put CONNECTIONSTRINGS__DEFAULTCONNECTION "${CONNECTION_STRING}"
  put JWT__KEY "${JWT_KEY}"
  put JWT__ISSUER "${JWT__ISSUER:-TutorSphere}"
  put JWT__AUDIENCE "${JWT__AUDIENCE:-TutorSphere}"
  put PAYGATEWAY__BASEURL "${PAYGATEWAY_BASE_URL}"
  put PAYGATEWAY__APPCODE "${PAYGATEWAY_APP_CODE:-TUTORSPHERE}"
  put PAYGATEWAY__APIKEY "${PAYGATEWAY_API_KEY}"
  # true = Stripe Test (X-Stripe-Env: DEV) ; false = Stripe Live (pas de header)
  # Défaut true tant que les paiements réels ne sont pas activés.
  put PAYGATEWAY__USESANDBOX "${PAYGATEWAY_USE_SANDBOX:-true}"
  put EMAIL__BASEURL "${EMAIL_BASE_URL:-https://gisemailsender.gisebs.com}"
  put EMAIL__APIKEY "${EMAIL_API_KEY:-}"
  put EMAIL__CLIENTCODE "${EMAIL_CLIENT_CODE:-TUTORSPHERE}"
  put APIBASEURL "${API_BASE_URL:-https://api.tutorsphere.gisebs.com}"
  put WEBBASEURL "${WEB_BASE_URL:-https://tutorsphere.gisebs.com}"
  printf 'INTERNALAPIBASEURL=http://127.0.0.1:%s\n' "${API_PORT:-55099}"
  printf 'API_PORT=%s\n' "${API_PORT:-55099}"
  printf 'WEB_PORT=%s\n' "${WEB_PORT:-55010}"

  # SuperAdmin bootstrap — valeurs prédéfinies (surchargeables via env / secrets GitHub)
  # Secret GitHub vide = "" : forcer les défauts ( ${VAR:-x} ne marche pas si VAR est définie vide ).
  _bs_enabled="${SEED_BOOTSTRAP_ADMIN_ENABLED:-false}"
  _bs_email="${SEED_BOOTSTRAP_ADMIN_EMAIL:-}"
  _bs_password="${SEED_BOOTSTRAP_ADMIN_PASSWORD:-}"
  [ -z "$_bs_email" ] && _bs_email="tutorsphere@gisebs.com"
  [ -z "$_bs_password" ] && _bs_password="Mcd!123456789"
  put SEED__BOOTSTRAPADMIN__ENABLED "$_bs_enabled"
  put SEED__BOOTSTRAPADMIN__EMAIL "$_bs_email"
  put SEED__BOOTSTRAPADMIN__PASSWORD "$_bs_password"
  put SEED__BOOTSTRAPADMIN__FIRSTNAME "${SEED_BOOTSTRAP_ADMIN_FIRSTNAME:-Admin}"
  put SEED__BOOTSTRAPADMIN__LASTNAME "${SEED_BOOTSTRAP_ADMIN_LASTNAME:-TutorSphere}"
} >> "$OUT"

chmod 600 "$OUT"
echo "Fichier .env généré : ${OUT}"
