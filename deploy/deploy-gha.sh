#!/usr/bin/env bash
# Déploiement TutorSphere depuis GitHub Actions vers Ubuntu (Docker Compose).
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
: "${SSH_PORT:=22}"
: "${API_PORT:=55099}"
: "${WEB_PORT:=55010}"
: "${APP_ENV_FILE:?APP_ENV_FILE requis}"
: "${COMPOSE_PROJECT_NAME:=tutorsphere}"

sanitize() {
  printf '%s' "$1" | tr -d '\r\n\t' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"//' -e 's/"$//' -e "s/^'//" -e "s/'$//"
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
JWT__ISSUER=$(sanitize "${JWT__ISSUER:-TutorSphere}")
JWT__AUDIENCE=$(sanitize "${JWT__AUDIENCE:-TutorSphere}")
API_BASE_URL=$(sanitize "${API_BASE_URL:-https://api.tutorsphere.gisebs.com}")

PG_URL_LOWER=$(printf '%s' "${PAYGATEWAY_BASE_URL}" | tr '[:upper:]' '[:lower:]')
if [[ "${PG_URL_LOWER}" == *giseboutique* ]] || [[ "${PG_URL_LOWER}" == *agentiamarket* ]]; then
  echo "::error::PayGateway BaseUrl incorrecte : ${PAYGATEWAY_BASE_URL}" >&2
  echo "→ Utilisez https://gisebsapipaygateway.gisebs.com (GISEBS Pay Gateway)." >&2
  exit 1
fi

if [[ ! "$SSH_HOST" =~ ^[0-9a-zA-Z.-]+$ ]]; then
  echo "SSH_HOST invalide" >&2
  exit 1
fi

[[ -f "${APP_ENV_FILE}" ]] || { echo "Fichier env introuvable : ${APP_ENV_FILE}" >&2; exit 1; }

APP_DIR="${APP_ROOT}/app"
BACKUP_DIR="${APP_ROOT}/backups"
STAGING_REMOTE="/tmp/tutorsphere-gha-$(date +%Y%m%d-%H%M%S)"
SSH_OPTS=(-p "${SSH_PORT}" -o BatchMode=yes -o StrictHostKeyChecking=yes)
SCP_OPTS=(-P "${SSH_PORT}" -o BatchMode=yes -o StrictHostKeyChecking=yes)
SSH_TARGET="${SSH_USER}@${SSH_HOST}"

if [[ -n "${SSH_KEY_PATH:-}" ]]; then
  SSH_OPTS+=(-i "${SSH_KEY_PATH}")
  SCP_OPTS+=(-i "${SSH_KEY_PATH}")
fi

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "sudo mkdir -p '${APP_DIR}' '${BACKUP_DIR}' '${APP_ROOT}' && sudo chown -R ${SSH_USER}:${SSH_USER} '${APP_ROOT}'"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_BACKUP
set -eu
TIMESTAMP=\$(date +%Y%m%d-%H%M%S)
if [[ -f '${APP_DIR}/.env' ]]; then
  cp '${APP_DIR}/.env' '${BACKUP_DIR}/env.'\${TIMESTAMP}
  echo "Sauvegarde env : ${BACKUP_DIR}/env.\${TIMESTAMP}"
fi
REMOTE_BACKUP

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "mkdir -p '${STAGING_REMOTE}'"

RSYNC_EXCLUDES=(
  --exclude '.git'
  --exclude '**/bin'
  --exclude '**/obj'
  --exclude 'tests'
  --exclude '.github'
  --exclude 'publish'
)

rsync -az "${RSYNC_EXCLUDES[@]}" \
  "${REPO_ROOT}/src" \
  "${REPO_ROOT}/docker-compose.yml" \
  "${REPO_ROOT}/docker-compose.prod.yml" \
  "${REPO_ROOT}/.dockerignore" \
  "${REPO_ROOT}/TutorSphere.slnx" \
  "${SSH_TARGET}:${STAGING_REMOTE}/"

scp "${SCP_OPTS[@]}" "${APP_ENV_FILE}" "${SSH_TARGET}:/tmp/tutorsphere.app.env"

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_DEPLOY
set -eu

echo "Arrêt conteneurs TutorSphere..."
cd '${APP_DIR}' 2>/dev/null || true
if [[ -f docker-compose.yml ]]; then
  docker compose -f docker-compose.yml -f docker-compose.prod.yml down --remove-orphans 2>/dev/null || \
    /usr/local/bin/docker-compose -f docker-compose.yml -f docker-compose.prod.yml down --remove-orphans 2>/dev/null || true
fi

sudo rsync -a --delete \
  --exclude '.env' \
  '${STAGING_REMOTE}/' '${APP_DIR}/'
rm -rf '${STAGING_REMOTE}'

sudo mv /tmp/tutorsphere.app.env '${APP_DIR}/.env'
sudo chmod 600 '${APP_DIR}/.env'
sudo chown ${SSH_USER}:${SSH_USER} '${APP_DIR}/.env'

cd '${APP_DIR}'
export COMPOSE_PROJECT_NAME='${COMPOSE_PROJECT_NAME}'

echo "Build images Docker..."
docker compose -f docker-compose.yml -f docker-compose.prod.yml build --pull || \
  /usr/local/bin/docker-compose -f docker-compose.yml -f docker-compose.prod.yml build --pull

echo "Démarrage conteneurs..."
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans || \
  /usr/local/bin/docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans

docker compose -f docker-compose.yml -f docker-compose.prod.yml ps || \
  /usr/local/bin/docker-compose -f docker-compose.yml -f docker-compose.prod.yml ps
REMOTE_DEPLOY

echo "Attente démarrage TutorSphere (migrations EF au boot)..."
ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" bash -s <<REMOTE_HEALTH
set -eu
API_PORT='${API_PORT}'
WEB_PORT='${WEB_PORT}'
for i in \$(seq 1 45); do
  API_CODE=\$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:\${API_PORT}/health" 2>/dev/null || echo "000")
  WEB_CODE=\$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:\${WEB_PORT}/health" 2>/dev/null || echo "000")
  if [ "\${API_CODE}" = "200" ] && [ "\${WEB_CODE}" = "200" ]; then
    # NPM (conteneur Docker) joint l'hôte via 172.17.0.1 — pas via loopback seul
    NPM_API=\$(curl -s -o /dev/null -w '%{http_code}' "http://172.17.0.1:\${API_PORT}/health" 2>/dev/null || echo "000")
    NPM_WEB=\$(curl -s -o /dev/null -w '%{http_code}' "http://172.17.0.1:\${WEB_PORT}/health" 2>/dev/null || echo "000")
    if [ "\${NPM_API}" = "200" ] && [ "\${NPM_WEB}" = "200" ]; then
      echo "Healthcheck API/Web OK (127.0.0.1 + 172.17.0.1) après \${i} tentative(s)"
      exit 0
    fi
    echo "127.0.0.1 OK mais 172.17.0.1 échoue (API \${NPM_API}, Web \${NPM_WEB}) — NPM renverra 502"
    exit 1
  fi
  if [ "\${i}" -ge 3 ]; then
    echo "Tentative \${i} — API HTTP \${API_CODE}, Web HTTP \${WEB_CODE}"
    docker compose -f '${APP_DIR}/docker-compose.yml' -f '${APP_DIR}/docker-compose.prod.yml' ps 2>/dev/null || true
  fi
  sleep 2
done
echo "::error::TutorSphere ne répond pas sur /health après 90 s"
cd '${APP_DIR}'
export COMPOSE_PROJECT_NAME='${COMPOSE_PROJECT_NAME}'
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs --tail=40 2>/dev/null || \
  /usr/local/bin/docker-compose -f docker-compose.yml -f docker-compose.prod.yml logs --tail=40 2>/dev/null || true
exit 1
REMOTE_HEALTH

echo "Déploiement TutorSphere réussi sur ${SSH_HOST} (Docker Compose, ports ${API_PORT}/${WEB_PORT})."
