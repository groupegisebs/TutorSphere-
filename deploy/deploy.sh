#!/usr/bin/env bash
# Déploiement manuel TutorSphere sur le serveur Linux (Docker Compose).
#
# Modes :
#   1) Sur le serveur après git pull (depuis la racine du dépôt) :
#        ./deploy/deploy.sh
#
#   2) Avec un répertoire .env déjà présent sous APP_ROOT/app/.env
#
# Variables d'environnement (surchargent les défauts) :
#   APP_ROOT, API_PORT, WEB_PORT, COMPOSE_PROJECT_NAME, SKIP_BUILD

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

APP_ROOT="${APP_ROOT:-/opt/apps/tutorsphere}"
APP_DIR="${APP_ROOT}/app"
API_PORT="${API_PORT:-55099}"
WEB_PORT="${WEB_PORT:-55010}"
COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-tutorsphere}"
SKIP_BUILD="${SKIP_BUILD:-0}"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
die() { log "ERREUR: $*" >&2; exit 1; }

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "Commande introuvable : $1"
}

compose() {
  if docker compose version >/dev/null 2>&1; then
    docker compose -f "${APP_DIR}/docker-compose.yml" -f "${APP_DIR}/docker-compose.prod.yml" "$@"
  elif command -v docker-compose >/dev/null 2>&1; then
    docker-compose -f "${APP_DIR}/docker-compose.yml" -f "${APP_DIR}/docker-compose.prod.yml" "$@"
  else
    die "Docker Compose introuvable (docker compose ou docker-compose)"
  fi
}

require_cmd docker
require_cmd rsync

log "TutorSphere — déploiement Docker vers ${APP_DIR}"

mkdir -p "${APP_DIR}"

if [[ ! -f "${APP_DIR}/.env" ]]; then
  if [[ -f "${REPO_ROOT}/deploy/env.example" ]]; then
    die "Créez ${APP_DIR}/.env à partir de deploy/env.example (secrets non versionnés)."
  fi
  die "Fichier ${APP_DIR}/.env manquant."
fi

log "Synchronisation des fichiers de build..."
rsync -a --delete \
  --exclude '.env' \
  --exclude '**/bin' \
  --exclude '**/obj' \
  "${REPO_ROOT}/src" \
  "${REPO_ROOT}/docker-compose.yml" \
  "${REPO_ROOT}/docker-compose.prod.yml" \
  "${REPO_ROOT}/.dockerignore" \
  "${REPO_ROOT}/TutorSphere.slnx" \
  "${APP_DIR}/"

cd "${APP_DIR}"
export COMPOSE_PROJECT_NAME

if [[ "${SKIP_BUILD}" != "1" ]]; then
  log "Build images..."
  compose build --pull
else
  log "SKIP_BUILD=1 — images non reconstruites."
fi

log "Démarrage conteneurs..."
compose up -d --remove-orphans
compose ps

log "Healthcheck..."
sleep 3
curl -fsS "http://127.0.0.1:${API_PORT}/health" >/dev/null || die "API /health échoué"
curl -fsS "http://127.0.0.1:${WEB_PORT}/health" >/dev/null || die "Web /health échoué"

log "Déploiement terminé avec succès."
