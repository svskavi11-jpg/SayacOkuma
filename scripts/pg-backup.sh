#!/bin/sh
# Westalife — PostgreSQL gece yedeği (.sql.gz), evrak arşivi (.tar.gz) ve 30 gün saklama
set -eu

BACKUP_DIR="${BACKUP_DIR:-/backups}"
DOCUMENTS_DIR="${DOCUMENTS_DIR:-/documents}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"
PREFIX="${BACKUP_PREFIX:-sayacokuma}"
STAMP="$(date +%Y%m%d_%H%M%S)"
FILE="${BACKUP_DIR}/${PREFIX}_${STAMP}.sql.gz"
DOC_FILE="${BACKUP_DIR}/documents_${STAMP}.tar.gz"

mkdir -p "${BACKUP_DIR}"

: "${POSTGRES_HOST:=db}"
: "${POSTGRES_PORT:=5432}"
: "${POSTGRES_DB:?POSTGRES_DB zorunlu}"
: "${POSTGRES_USER:?POSTGRES_USER zorunlu}"
: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD zorunlu}"

export PGPASSWORD="${POSTGRES_PASSWORD}"

echo "[$(date -Iseconds)] Yedek basliyor: ${FILE}"

if ! pg_dump \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --format=plain \
  --no-owner \
  --no-acl \
  | gzip -c > "${FILE}.partial"
then
  echo "[$(date -Iseconds)] HATA: pg_dump basarisiz"
  rm -f "${FILE}.partial"
  exit 1
fi

mv "${FILE}.partial" "${FILE}"
SIZE="$(wc -c < "${FILE}" | tr -d ' ')"
echo "[$(date -Iseconds)] Yedek tamam: ${FILE} (${SIZE} bayt)"

echo "[$(date -Iseconds)] Evrak yedegi basliyor: ${DOC_FILE}"

if ! tar -czf "${DOC_FILE}.partial" -C "${DOCUMENTS_DIR}" .
then
  echo "[$(date -Iseconds)] HATA: evrak arsivi basarisiz"
  rm -f "${DOC_FILE}.partial"
  exit 1
fi

mv "${DOC_FILE}.partial" "${DOC_FILE}"
DOC_SIZE="$(wc -c < "${DOC_FILE}" | tr -d ' ')"
echo "[$(date -Iseconds)] Evrak yedegi tamam: ${DOC_FILE} (${DOC_SIZE} bayt)"

DELETED_SQL="$(find "${BACKUP_DIR}" -type f -name "${PREFIX}_*.sql.gz" -mtime +"${RETENTION_DAYS}" -print -delete | wc -l | tr -d ' ')"
DELETED_DOCS="$(find "${BACKUP_DIR}" -type f -name "documents_*.tar.gz" -mtime +"${RETENTION_DAYS}" -print -delete | wc -l | tr -d ' ')"
echo "[$(date -Iseconds)] Retention: ${RETENTION_DAYS} gunden eski ${DELETED_SQL} sql ve ${DELETED_DOCS} evrak yedegi silindi"
