#!/usr/bin/env bash
# Déploiement TutorSphere depuis GitHub Actions vers Ubuntu (Docker Compose).
# Même principe que BoutiqueGisie : scp + clé SSH, rsync sur le serveur uniquement.
# Secrets : voir deploy/GITHUB-SECRETS.md

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
# shellcheck source=deploy/database.defaults.sh
source "${SCRIPT_DIR}/database.defaults.sh"

: "${SSH_HOST:?SSH_HOST requis}"
: "${SSH_USER:?SSH_USER requis}"
: "${APP_ROOT:?APP_ROOT requis}"
: "${CONNECTION_STRING:?CONNECTION_STRING requis}"
: "${JWT_KEY:?JWT_KEY requis}"
: "${PAYGATEWAY_BASE_URL:?PAYGATEWAY_BASE_URL requis}"
: "${PAYGATEWAY_API_KEY:?PAYGATEWAY_API_KEY requis}"
: "${EMAIL_API_KEY:=}"
: "${EMAIL_CLIENT_CODE:=TUTORSPHERE}"
: "${SSH_PORT:=22}"
: "${API_PORT:=55099}"
: "${WEB_PORT:=55010}"
: "${COMPOSE_PROJECT_NAME:=tutorsphere}"

sanitize() {
  printf '%s' "$1" | tr -d '\r\n\t' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
}

die() {
  echo "::error::$*" >&2
  exit 1
}

SSH_HOST=$(sanitize "${SSH_HOST}")
SSH_HOST="${SSH_HOST#http://}"
SSH_HOST="${SSH_HOST#https://}"
SSH_HOST="${SSH_HOST%%/*}"
SSH_USER=$(sanitize "${SSH_USER}")
SSH_PORT=$(sanitize "${SSH_PORT}")
APP_ROOT=$(sanitize "${APP_ROOT}")
API_PORT=$(sanitize "${API_PORT}")
WEB_PORT=$(sanitize "${WEB_PORT}")
CONNECTION_STRING=$(sanitize "${CONNECTION_STRING}")
CONNECTION_STRING=$(normalize_tutorsphere_connection_string "${CONNECTION_STRING}")
PAYGATEWAY_BASE_URL=$(sanitize "${PAYGATEWAY_BASE_URL}")
PAYGATEWAY_BASE_URL="${PAYGATEWAY_BASE_URL%/}"
PAYGATEWAY_API_KEY=$(sanitize "${PAYGATEWAY_API_KEY}")
PAYGATEWAY_APP_CODE=$(sanitize "${PAYGATEWAY_APP_CODE:-TUTORSPHERE}")
JWT_KEY=$(sanitize "${JWT_KEY}")
API_BASE_URL=$(sanitize "${API_BASE_URL:-https://api.tutorsphere.gisebs.com}")

PG_URL_LOWER=$(printf '%s' "${PAYGATEWAY_BASE_URL}" | tr '[:upper:]' '[:lower:]')
if [[ "${PG_URL_LOWER}" == *giseboutique* ]] || [[ "${PG_URL_LOWER}" == *agentiamarket* ]]; then
  die "PayGateway BaseUrl incorrecte : ${PAYGATEWAY_BASE_URL} — utilisez https://gisebsapipaygateway.gisebs.com"
fi

if [[ ! "$SSH_HOST" =~ ^[0-9a-zA-Z.-]+$ ]]; then
  die "SSH_HOST invalide : ${SSH_HOST}"
fi

APP_DIR="${APP_ROOT}/app"
BACKUP_DIR="${APP_ROOT}/backups"
STAGING_REMOTE="/tmp/tutorsphere-gha-$(date +%Y%m%d-%H%M%S)"
SSH_OPTS=(-p "${SSH_PORT}" -o BatchMode=yes -o StrictHostKeyChecking=yes)
SCP_OPTS=(-P "${SSH_PORT}" -o BatchMode=yes -o StrictHostKeyChecking=yes)
SSH_TARGET="${SSH_USER}@${SSH_HOST}"

if [[ -n "${SSH_KEY_PATH:-}" ]]; then
  [[ -f "${SSH_KEY_PATH}" ]] || die "Clé SSH introuvable : ${SSH_KEY_PATH}"
  SSH_OPTS+=(-i "${SSH_KEY_PATH}")
  SCP_OPTS+=(-i "${SSH_KEY_PATH}")
else
  die "SSH_KEY_PATH requis pour scp/ssh depuis GitHub Actions"
fi

DB_NAME="$(printf '%s' "${CONNECTION_STRING}" | sed -n 's/.*[Dd]atabase=\([^;]*\).*/\1/p')"
DB_NAME="${DB_NAME:-${TUTORSPHERE_POSTGRES_DATABASE}}"
DB_OWNER="$(printf '%s' "${CONNECTION_STRING}" | sed -n 's/.*[Uu]ser [Ii][Dd]=\([^;]*\).*/\1/p')"
if [[ -z "${DB_OWNER}" ]]; then
  DB_OWNER="$(printf '%s' "${CONNECTION_STRING}" | sed -n 's/.*[Uu]sername=\([^;]*\).*/\1/p')"
fi
DB_OWNER="${DB_OWNER:-gisedocuser}"

verify_remote_staging() {
  ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<VERIFY
set -eu
STAGING='${STAGING_REMOTE}'
missing=0
for path in docker-compose.yml docker-compose.prod.yml .dockerignore TutorSphere.slnx src/TutorSphere.Api/TutorSphere.Api.csproj; do
  if [[ ! -e "\${STAGING}/\${path}" ]]; then
    echo "Manquant après scp : \${STAGING}/\${path}" >&2
    missing=1
  fi
done
file_count=\$(find "\${STAGING}" -type f 2>/dev/null | wc -l | tr -d ' ')
if [[ "\${file_count}" -lt 10 ]]; then
  echo "Staging incomplet : seulement \${file_count} fichier(s) dans \${STAGING}" >&2
  ls -la "\${STAGING}" 2>/dev/null || true
  missing=1
fi
[[ "\${missing}" -eq 0 ]] || exit 1
echo "Staging OK : \${file_count} fichiers dans \${STAGING}"
VERIFY
}

verify_remote_app() {
  ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<VERIFY
set -eu
APP='${APP_DIR}'
missing=0
for path in docker-compose.yml docker-compose.prod.yml .env src/TutorSphere.Api/TutorSphere.Api.csproj; do
  if [[ ! -e "\${APP}/\${path}" ]]; then
    echo "Manquant dans app : \${APP}/\${path}" >&2
    missing=1
  fi
done
file_count=\$(find "\${APP}" -type f 2>/dev/null | wc -l | tr -d ' ')
if [[ "\${file_count}" -lt 10 ]]; then
  echo "Répertoire app incomplet : seulement \${file_count} fichier(s) dans \${APP}" >&2
  ls -la "\${APP}" 2>/dev/null || true
  missing=1
fi
[[ "\${missing}" -eq 0 ]] || exit 1
echo "App OK : \${file_count} fichiers dans \${APP}"
VERIFY
}

echo "Cible : ${SSH_USER}@${SSH_HOST}:${SSH_PORT} → ${APP_DIR}"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "sudo mkdir -p '${APP_DIR}' '${BACKUP_DIR}' '${APP_ROOT}' && sudo chown -R ${SSH_USER}:${SSH_USER} '${APP_ROOT}'"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_BACKUP
set -eu
TIMESTAMP=\$(date +%Y%m%d-%H%M%S)
if [[ -f '${APP_DIR}/.env' ]]; then
  cp '${APP_DIR}/.env' '${BACKUP_DIR}/env.'\${TIMESTAMP}
  echo "Sauvegarde env : ${BACKUP_DIR}/env.\${TIMESTAMP}"
fi
REMOTE_BACKUP

STAGING_LOCAL="$(mktemp -d)"
trap 'rm -rf "${STAGING_LOCAL}"' EXIT

rsync -a \
  --exclude '.git' \
  --exclude '**/bin' \
  --exclude '**/obj' \
  --exclude 'tests' \
  --exclude '.github' \
  --exclude 'publish' \
  "${REPO_ROOT}/src" \
  "${REPO_ROOT}/docker-compose.yml" \
  "${REPO_ROOT}/docker-compose.prod.yml" \
  "${REPO_ROOT}/.dockerignore" \
  "${REPO_ROOT}/TutorSphere.slnx" \
  "${STAGING_LOCAL}/"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "mkdir -p '${STAGING_REMOTE}'"
echo "Transfert sources vers ${STAGING_REMOTE} (scp)..."
scp "${SCP_OPTS[@]}" -r "${STAGING_LOCAL}/." "${SSH_TARGET}:${STAGING_REMOTE}/"

verify_remote_staging

ENV_FILE="$(mktemp)"
trap 'rm -f "${ENV_FILE}"; rm -rf "${STAGING_LOCAL}"' EXIT
CONNECTION_STRING="${CONNECTION_STRING}" \
JWT_KEY="${JWT_KEY}" \
PAYGATEWAY_BASE_URL="${PAYGATEWAY_BASE_URL}" \
PAYGATEWAY_API_KEY="${PAYGATEWAY_API_KEY}" \
PAYGATEWAY_APP_CODE="${PAYGATEWAY_APP_CODE}" \
PAYGATEWAY_USE_SANDBOX="${PAYGATEWAY_USE_SANDBOX:-true}" \
EMAIL_API_KEY="${EMAIL_API_KEY}" \
EMAIL_CLIENT_CODE="${EMAIL_CLIENT_CODE}" \
API_BASE_URL="${API_BASE_URL}" \
API_PORT="${API_PORT}" \
WEB_PORT="${WEB_PORT}" \
bash "${SCRIPT_DIR}/build-app-env.sh" "${ENV_FILE}"

scp "${SCP_OPTS[@]}" "${ENV_FILE}" "${SSH_TARGET}:/tmp/tutorsphere.app.env"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_DEPLOY
set -eu

resolve_compose() {
  if docker compose version >/dev/null 2>&1; then
    echo "docker compose"
  elif command -v docker-compose >/dev/null 2>&1; then
    echo "docker-compose"
  else
    echo "::error::Docker Compose introuvable — installez le plugin 'docker compose' ou le binaire 'docker-compose'" >&2
    exit 1
  fi
}

COMPOSE_BIN=\$(resolve_compose)
echo "Docker Compose : \${COMPOSE_BIN}"

compose() {
  if [[ "\${COMPOSE_BIN}" == "docker compose" ]]; then
    docker compose -f docker-compose.yml -f docker-compose.prod.yml "\$@"
  else
    docker-compose -f docker-compose.yml -f docker-compose.prod.yml "\$@"
  fi
}

DB_NAME='${DB_NAME}'
DB_OWNER='${DB_OWNER}'
if command -v psql >/dev/null 2>&1; then
  if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='\${DB_NAME}'" | grep -q 1; then
    echo "Création base \${DB_NAME}..."
    sudo -u postgres psql -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"\${DB_NAME}\" OWNER \${DB_OWNER};" || true
  fi
  sudo -u postgres psql -d "\${DB_NAME}" -v ON_ERROR_STOP=1 -c "GRANT ALL ON SCHEMA public TO \${DB_OWNER};" || true
fi

echo "Arrêt conteneurs TutorSphere..."
cd '${APP_DIR}' 2>/dev/null || true
if [[ -f docker-compose.yml ]]; then
  compose down --remove-orphans 2>/dev/null || true
fi

echo "Copie staging → ${APP_DIR}..."
sudo rsync -a --delete \
  --exclude '.env' \
  '${STAGING_REMOTE}/' '${APP_DIR}/'
rm -rf '${STAGING_REMOTE}'

sudo mv /tmp/tutorsphere.app.env '${APP_DIR}/.env'
sudo chmod 600 '${APP_DIR}/.env'
sudo chown ${SSH_USER}:${SSH_USER} '${APP_DIR}/.env'
sudo chown -R ${SSH_USER}:${SSH_USER} '${APP_DIR}'

cd '${APP_DIR}'
export COMPOSE_PROJECT_NAME='${COMPOSE_PROJECT_NAME}'

echo "Build images Docker..."
compose build --pull

echo "Démarrage conteneurs..."
compose up -d --remove-orphans

compose ps
REMOTE_DEPLOY

verify_remote_app

echo "Attente démarrage TutorSphere (migrations EF au boot)..."
ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_HEALTH
set -eu

resolve_compose() {
  if docker compose version >/dev/null 2>&1; then
    echo "docker compose"
  elif command -v docker-compose >/dev/null 2>&1; then
    echo "docker-compose"
  else
    echo "docker compose"
  fi
}

COMPOSE_BIN=\$(resolve_compose)
compose() {
  if [[ "\${COMPOSE_BIN}" == "docker compose" ]]; then
    docker compose -f '${APP_DIR}/docker-compose.yml' -f '${APP_DIR}/docker-compose.prod.yml' "\$@"
  else
    docker-compose -f '${APP_DIR}/docker-compose.yml' -f '${APP_DIR}/docker-compose.prod.yml' "\$@"
  fi
}

API_PORT='${API_PORT}'
WEB_PORT='${WEB_PORT}'
for i in \$(seq 1 45); do
  API_CODE=\$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:\${API_PORT}/health" 2>/dev/null || echo "000")
  WEB_CODE=\$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:\${WEB_PORT}/health" 2>/dev/null || echo "000")
  if [ "\${API_CODE}" = "200" ] && [ "\${WEB_CODE}" = "200" ]; then
    NPM_API=\$(curl -s -o /dev/null -w '%{http_code}' "http://172.17.0.1:\${API_PORT}/health" 2>/dev/null || echo "000")
    NPM_WEB=\$(curl -s -o /dev/null -w '%{http_code}' "http://172.17.0.1:\${WEB_PORT}/health" 2>/dev/null || echo "000")
    if [ "\${NPM_API}" = "200" ] && [ "\${NPM_WEB}" = "200" ]; then
      echo "Healthcheck API/Web OK (127.0.0.1 + 172.17.0.1) après \${i} tentative(s)"
      exit 0
    fi
    echo "::error::127.0.0.1 OK mais 172.17.0.1 échoue (API \${NPM_API}, Web \${NPM_WEB}) — NPM renverra 502" >&2
    echo "→ Vérifiez que docker-compose.prod.yml bind 0.0.0.0:\${WEB_PORT}/\${API_PORT}" >&2
    exit 1
  fi
  if [ "\${i}" -ge 3 ]; then
    echo "Tentative \${i} — API HTTP \${API_CODE}, Web HTTP \${WEB_CODE}"
    compose ps 2>/dev/null || true
  fi
  sleep 2
done
echo "::error::TutorSphere ne répond pas sur /health après 90 s" >&2
cd '${APP_DIR}'
export COMPOSE_PROJECT_NAME='${COMPOSE_PROJECT_NAME}'
compose logs --tail=40 2>/dev/null || true
exit 1
REMOTE_HEALTH

echo "Déploiement TutorSphere réussi sur ${SSH_HOST} (Docker Compose, ports ${API_PORT}/${WEB_PORT})."
