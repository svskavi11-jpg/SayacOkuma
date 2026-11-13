#!/usr/bin/env bash
# Kaynak makinede calisir. Calisan uc imaji, veritabani dump'ini ve evraklari pakete koyar.
# manifest.json her dosyanin SHA-256 degerini ve kayit sayilarini tutar.
# Gizli anahtarlar manifestte yazmaz; secrets.env icindedir.
#
# Paket sirasinda tahsilat ve evrak yuklemeyin. Dump anlik goruntudur.
#
# Calistirma (deponun kokunden, uretim yigini ayaktayken):
#   bash scripts/package-source-server.sh
set -euo pipefail

export MSYS_NO_PATHCONV=1

die() {
  echo "HATA: $*" >&2
  exit 1
}

hash_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    die "sha256sum veya shasum yok."
  fi
}

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

command -v docker >/dev/null 2>&1 || die "docker yok."
docker info >/dev/null 2>&1 || die "Docker calismiyor."
command -v gzip >/dev/null 2>&1 || die "gzip yok."

DB_CONTAINER=sayacokuma-prod-db
API_CONTAINER=sayacokuma-prod-api
PG_VOLUME=sayacokuma-prod_sayacokuma_pgdata_prod
DOC_VOLUME=sayacokuma-prod_sayacokuma_resident_documents
IMAGES=(
  sayacokuma-api:latest
  sayacokuma-web:latest
  sayacokuma-db-backup:latest
)

status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$DB_CONTAINER" 2>/dev/null || true)"
[ "$status" = "healthy" ] || die "Veritabani saglikli degil (${status:-yok}). Uretim yiginini acin."
docker volume inspect "$DOC_VOLUME" >/dev/null 2>&1 || die "Evrak volume bulunamadi: ${DOC_VOLUME}"
docker volume inspect "$PG_VOLUME" >/dev/null 2>&1 || die "Veritabani volume bulunamadi: ${PG_VOLUME}"

for image in "${IMAGES[@]}"; do
  docker image inspect "$image" >/dev/null 2>&1 || die "Imaj yok: ${image}. Once imaji derleyin."
done

STAMP="$(date +%Y%m%d_%H%M%S)"
BUNDLE="${ROOT}/migration-out/${STAMP}"
mkdir -p "${BUNDLE}/images"
chmod 700 "${ROOT}/migration-out" "${BUNDLE}" "${BUNDLE}/images"

cleanup() {
  find "$BUNDLE" -name '*.partial' -type f -delete 2>/dev/null || true
}
trap cleanup EXIT

echo "Kayit sayilari okunuyor..."
sql_count() {
  printf '%s\n' "$1" | docker exec -i "$DB_CONTAINER" \
    sh -c 'export PGPASSWORD="$POSTGRES_PASSWORD"; exec psql -v ON_ERROR_STOP=1 -At --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' \
    | tr -d '[:space:]'
}

apartments="$(sql_count 'SELECT COUNT(*) FROM "Apartments"')"
payments="$(sql_count 'SELECT COUNT(*) FROM "Payments"')"
resident_documents="$(sql_count 'SELECT COUNT(*) FROM "ResidentDocuments"')"
[[ "$apartments" =~ ^[0-9]+$ ]] || die "Daire sayisi okunamadi."
[[ "$payments" =~ ^[0-9]+$ ]] || die "Tahsilat sayisi okunamadi."
[[ "$resident_documents" =~ ^[0-9]+$ ]] || die "Evrak sayisi okunamadi."

echo "Veritabani paketleniyor..."
if ! docker exec "$DB_CONTAINER" \
  sh -c 'export PGPASSWORD="$POSTGRES_PASSWORD"; exec pg_dump --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --format=plain --no-owner --no-acl --encoding=UTF8' \
  | gzip -c > "${BUNDLE}/database.sql.gz.partial"
then
  die "pg_dump basarisiz."
fi
mv "${BUNDLE}/database.sql.gz.partial" "${BUNDLE}/database.sql.gz"
gzip -t "${BUNDLE}/database.sql.gz" || die "database.sql.gz bozuk."
chmod 600 "${BUNDLE}/database.sql.gz"

echo "Evraklar paketleniyor..."
if ! docker run --rm -v "${DOC_VOLUME}:/documents:ro" postgres:18-alpine \
  tar -czf - -C /documents . > "${BUNDLE}/documents.tar.gz.partial"
then
  die "Evrak arsivi basarisiz."
fi
mv "${BUNDLE}/documents.tar.gz.partial" "${BUNDLE}/documents.tar.gz"
tar -tzf "${BUNDLE}/documents.tar.gz" >/dev/null || die "documents.tar.gz bozuk."
chmod 600 "${BUNDLE}/documents.tar.gz"

document_files="$(docker run --rm -v "${DOC_VOLUME}:/documents:ro" postgres:18-alpine find /documents -type f | wc -l | tr -d '[:space:]')"
document_bytes="$(docker run --rm -v "${DOC_VOLUME}:/documents:ro" postgres:18-alpine sh -c 'sum=0; for f in $(find /documents -type f); do c=$(wc -c < "$f"); sum=$((sum + c)); done; echo "$sum"' | tr -d '[:space:]')"
[[ "$document_files" =~ ^[0-9]+$ ]] || die "Evrak dosya sayisi okunamadi."
[[ "$document_bytes" =~ ^[0-9]+$ ]] || die "Evrak boyutu okunamadi."

read_container_env() {
  docker exec "$API_CONTAINER" printenv "$1" 2>/dev/null || true
}

signing_key="$(read_container_env AUTH_SIGNING_KEY)"
api_key="$(read_container_env SECURITY_API_KEY)"
if [ -z "$signing_key" ] || [ -z "$api_key" ]; then
  env_file="${ROOT}/.env"
  [ -f "$env_file" ] || die "Imza anahtari okunamadi. API ayakta degil ve .env yok."
  read_file_env() {
    local key="$1"
    local line
    line="$(grep -E "^${key}=" "$env_file" | tail -n 1 || true)"
    line="${line#*=}"
    line="${line%\"}"
    line="${line#\"}"
    printf '%s' "$line"
  }
  [ -n "$signing_key" ] || signing_key="$(read_file_env AUTH_SIGNING_KEY)"
  [ -n "$api_key" ] || api_key="$(read_file_env SECURITY_API_KEY)"
fi
[ -n "$signing_key" ] || die "AUTH_SIGNING_KEY bos."
[ -n "$api_key" ] || die "SECURITY_API_KEY bos."

umask 077
printf 'AUTH_SIGNING_KEY=%s\nSECURITY_API_KEY=%s\n' "$signing_key" "$api_key" > "${BUNDLE}/secrets.env"
chmod 600 "${BUNDLE}/secrets.env"
unset signing_key api_key
echo "Gizli anahtarlar pakete yazildi. Degerler terminale basilmadi."

for image in "${IMAGES[@]}"; do
  name="${image%%:*}"
  out="${BUNDLE}/images/${name}.tar"
  echo "Imaj kaydediliyor: ${image}"
  docker save -o "${out}.partial" "$image"
  mv "${out}.partial" "$out"
done

created_on="$(date -Iseconds 2>/dev/null || date '+%Y-%m-%dT%H:%M:%S%z')"
{
  echo '{'
  echo '  "version": 1,'
  printf '  "createdOn": "%s",\n' "$created_on"
  echo '  "files": ['
  first=1
  while IFS= read -r path; do
    [ -n "$path" ] || continue
    hash="$(hash_file "${BUNDLE}/${path}")"
    bytes="$(wc -c < "${BUNDLE}/${path}" | tr -d '[:space:]')"
    if [ "$first" -eq 0 ]; then
      echo ','
    fi
    first=0
    printf '    {"path":"%s","sha256":"%s","bytes":%s}' "$path" "$hash" "$bytes"
  done <<'FILES'
database.sql.gz
documents.tar.gz
images/sayacokuma-api.tar
images/sayacokuma-web.tar
images/sayacokuma-db-backup.tar
secrets.env
FILES
  echo
  echo '  ],'
  printf '  "counts": {"apartments":%s,"payments":%s,"residentDocuments":%s,"documentFiles":%s,"documentBytes":%s}\n' \
    "$apartments" "$payments" "$resident_documents" "$document_files" "$document_bytes"
  echo '}'
} > "${BUNDLE}/manifest.json"
chmod 600 "${BUNDLE}/manifest.json"

echo "Paket hazir: ${BUNDLE}"
echo "Daire: ${apartments}  Tahsilat: ${payments}  Evrak kaydi: ${resident_documents}  Evrak dosyasi: ${document_files}"
echo "Hedefte: sudo bash scripts/import-to-target.sh ${BUNDLE}"
