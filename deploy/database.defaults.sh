#!/usr/bin/env bash
# PostgreSQL TutorSphere — serveur 51.79.53.197, base dédiée TutorSphere.

TUTORSPHERE_DB_HOST="${TUTORSPHERE_DB_HOST:-51.79.53.197}"
TUTORSPHERE_DB_PORT="${TUTORSPHERE_DB_PORT:-5432}"
TUTORSPHERE_POSTGRES_DATABASE="${TUTORSPHERE_POSTGRES_DATABASE:-TutorSphere}"

merge_postgres_host() {
  local cs="$1"
  local host="$2"
  if [[ "$cs" =~ [Hh]ost= ]]; then
    printf '%s' "$cs" | sed -E "s/([;^]?)[Hh]ost=[^;]*/\\1Host=${host}/"
  else
    printf '%s' "Host=${host};${cs}"
  fi
}

merge_postgres_database() {
  local cs="$1"
  local db="$2"
  if [[ "$cs" =~ [Dd]atabase= ]]; then
    printf '%s' "$cs" | sed -E "s/([;^]?)[Dd]atabase=[^;]*/\\1Database=${db}/"
  else
    printf '%s;Database=%s' "$cs" "$db"
  fi
}

normalize_tutorsphere_connection_string() {
  local cs="$1"
  cs=$(merge_postgres_host "$cs" "$TUTORSPHERE_DB_HOST")
  merge_postgres_database "$cs" "$TUTORSPHERE_POSTGRES_DATABASE"
}
