#!/usr/bin/env bash
# Server-side deployment script for TutorSphere (API + Blazor Web).
# Invoked by GitHub Actions after rsync, or manually on the Linux host.
set -euo pipefail

DEPLOY_PATH="${DEPLOY_PATH:-/var/www/tutorsphere}"
API_PORT="${API_PORT:-55099}"
WEB_PORT="${WEB_PORT:-55010}"
SERVICE_USER="${SERVICE_USER:-tutorsphere}"
SYSTEMD_DIR="/etc/systemd/system"

log() { echo "[deploy] $*"; }

require_root() {
  if [[ "${EUID}" -ne 0 ]]; then
    log "This script must run as root (sudo) to install systemd units."
    exit 1
  fi
}

ensure_user() {
  if ! id "${SERVICE_USER}" &>/dev/null; then
    log "Creating system user ${SERVICE_USER}"
    useradd --system --home "${DEPLOY_PATH}" --shell /usr/sbin/nologin "${SERVICE_USER}"
  fi
}

write_env_file() {
  local env_file="${DEPLOY_PATH}/env"

  if [[ -f "${env_file}" && -z "${CONNECTIONSTRINGS__DEFAULTCONNECTION:-}" ]]; then
    log "Using existing environment file ${env_file}"
    chown root:"${SERVICE_USER}" "${env_file}"
    chmod 640 "${env_file}"
    return
  fi

  if [[ -z "${CONNECTIONSTRINGS__DEFAULTCONNECTION:-}" ]]; then
    log "Missing ${env_file} and CONNECTIONSTRINGS__DEFAULTCONNECTION is not set."
    exit 1
  fi

  log "Writing environment file ${env_file}"

  umask 077
  cat > "${env_file}" <<EOF
ASPNETCORE_ENVIRONMENT=Production
CONNECTIONSTRINGS__DEFAULTCONNECTION=${CONNECTIONSTRINGS__DEFAULTCONNECTION}
JWT__KEY=${JWT__KEY:-}
JWT__ISSUER=${JWT__ISSUER:-TutorSphere}
JWT__AUDIENCE=${JWT__AUDIENCE:-TutorSphere}
STRIPE__SECRETKEY=${STRIPE__SECRETKEY:-}
STRIPE__PUBLISHABLEKEY=${STRIPE__PUBLISHABLEKEY:-}
STRIPE__WEBHOOKSECRET=${STRIPE__WEBHOOKSECRET:-}
APIBASEURL=${APIBASEURL:-}
EOF

  chown root:"${SERVICE_USER}" "${env_file}"
  chmod 640 "${env_file}"
}

install_systemd_unit() {
  local template="$1"
  local unit_name="$2"
  local port="$3"
  local dll_name="$4"

  sed \
    -e "s|@DEPLOY_PATH@|${DEPLOY_PATH}|g" \
    -e "s|@SERVICE_USER@|${SERVICE_USER}|g" \
    -e "s|@PORT@|${port}|g" \
    -e "s|@DLL_NAME@|${dll_name}|g" \
    "${template}" > "${SYSTEMD_DIR}/${unit_name}"

  log "Installed ${SYSTEMD_DIR}/${unit_name}"
}

set_permissions() {
  chown -R "${SERVICE_USER}:${SERVICE_USER}" "${DEPLOY_PATH}/api" "${DEPLOY_PATH}/web"
  chmod +x "${DEPLOY_PATH}/api/TutorSphere.Api" 2>/dev/null || true
  chmod +x "${DEPLOY_PATH}/web/TutorSphere.Web" 2>/dev/null || true
}

restart_services() {
  systemctl daemon-reload
  systemctl enable tutorsphere-api.service tutorsphere-web.service
  systemctl restart tutorsphere-api.service
  systemctl restart tutorsphere-web.service
  log "Services restarted"
  systemctl --no-pager --full status tutorsphere-api.service tutorsphere-web.service || true
}

main() {
  require_root
  ensure_user

  mkdir -p "${DEPLOY_PATH}/api" "${DEPLOY_PATH}/web" "${DEPLOY_PATH}/deploy"

  if [[ ! -f "${DEPLOY_PATH}/api/TutorSphere.Api.dll" ]]; then
    log "Missing ${DEPLOY_PATH}/api/TutorSphere.Api.dll — run rsync from CI first."
    exit 1
  fi

  if [[ ! -f "${DEPLOY_PATH}/web/TutorSphere.Web.dll" ]]; then
    log "Missing ${DEPLOY_PATH}/web/TutorSphere.Web.dll — run rsync from CI first."
    exit 1
  fi

  write_env_file
  set_permissions

  install_systemd_unit \
    "${DEPLOY_PATH}/deploy/systemd/tutorsphere-api.service.template" \
    "tutorsphere-api.service" \
    "${API_PORT}" \
    "TutorSphere.Api.dll"

  install_systemd_unit \
    "${DEPLOY_PATH}/deploy/systemd/tutorsphere-web.service.template" \
    "tutorsphere-web.service" \
    "${WEB_PORT}" \
    "TutorSphere.Web.dll"

  restart_services
  log "Deployment complete."
}

main "$@"
