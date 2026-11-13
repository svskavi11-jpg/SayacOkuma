#!/usr/bin/env bash
# Hedef sunucuda calisir. Checksum tutmazsa veya sayilar uyusmazsa trafige acmaz.
# Veritabani zaten Westalife tablosu iceriyorsa yuklemeyi reddeder.
# Basaridan sonra imza anahtari ve API anahtari kaynaktaki degerlerle degisir.
# Veritabani sifresi hedefte uretilen sifre olarak kalir. API ve web acilmaz. DNS degismez.
#
# Calistirma:
#   sudo bash scripts/import-to-target.sh /yol/paket
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

sql_count() {
  printf '%s\n' "$1" | docker exec -i "$DB_CONTAINER" \
    sh -c 'export PGPASSWORD="$POSTGRES_PASSWORD"; exec psql -v ON_ERROR_STOP=1 -At --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' \
    | tr -d '[:space:]'
}

if [ "$(id -u)" -ne 0 ]; then
  die "Root olarak calistirin: sudo bash scripts/import-to-target.sh /yol/paket"
fi

[ $# -eq 1 ] || die "Kullanim: sudo bash scripts/import-to-target.sh /yol/paket"
BUNDLE="$(cd "$1" && pwd)"
[ -f "${BUNDLE}/manifest.json" ] || die "manifest.json yok: ${BUNDLE}"

DEST=/opt/westalife
COMPOSE="${DEST}/docker-compose.prod.yml"
ENV_FILE="${DEST}/.env"
DB_CONTAINER=sayacokuma-prod-db
DOC_VOLUME=sayacokuma-prod_sayacokuma_resident_documents

[ -f "$COMPOSE" ] || die "Once setup-target-server.sh calistirin. ${COMPOSE} yok."
[ -f "$ENV_FILE" ] || die "Once setup-target-server.sh calistirin. ${ENV_FILE} yok."
command -v docker >/dev/null 2>&1 || die "docker yok."
docker info >/dev/null 2>&1 || die "Docker calismiyor."
command -v gzip >/dev/null 2>&1 || die "gzip yok."

echo "Checksum dogrulaniyor..."
file_count=0
while IFS= read -r line; do
  path="$(printf '%s' "$line" | awk -F'"' '{print $4}')"
  expected="$(printf '%s' "$line" | awk -F'"' '{print $8}')"
  [ -n "$path" ] || die "Manifest satiri bozuk."
  [ -n "$expected" ] || die "Manifest ozeti bos: ${path}"
  [ -f "${BUNDLE}/${path}" ] || die "Paket dosyasi yok: ${path}"
  actual="$(hash_file "${BUNDLE}/${path}")"
  [ "$actual" = "$expected" ] || die "Checksum tutmadi: ${path}"
  file_count=$((file_count + 1))
done < <(grep -E '^[[:space:]]*\{"path":' "${BUNDLE}/manifest.json")
[ "$file_count" -ge 6 ] || die "Manifest eksik. ${file_count} dosya dogrulandi."

counts_line="$(grep -E '"counts":' "${BUNDLE}/manifest.json")"
count_of() {
  printf '%s' "$counts_line" | sed -n "s/.*\"$1\":\\([0-9][0-9]*\\).*/\\1/p"
}
expected_apartments="$(count_of apartments)"
expected_payments="$(count_of payments)"
expected_documents="$(count_of residentDocuments)"
expected_files="$(count_of documentFiles)"
expected_bytes="$(count_of documentBytes)"
for value in "$expected_apartments" "$expected_payments" "$expected_documents" "$expected_files" "$expected_bytes"; do
  [[ "$value" =~ ^[0-9]+$ ]] || die "Manifest sayilari okunamadi."
done

gzip -t "${BUNDLE}/database.sql.gz" || die "database.sql.gz bozuk."
tar -tzf "${BUNDLE}/documents.tar.gz" >/dev/null || die "documents.tar.gz bozuk."

status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$DB_CONTAINER" 2>/dev/null || true)"
[ "$status" = "healthy" ] || die "Hedef veritabani saglikli degil. Once setup-target-server.sh calistirin."
docker volume inspect "$DOC_VOLUME" >/dev/null 2>&1 || die "Evrak volume yok. Once bos veritabanini acin."

existing_tables="$(sql_count "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Apartments'")"
[ "$existing_tables" = "0" ] || die "Hedef veritabaninda Westalife tablosu var. Yukleme durdu. Mevcut veriye yazilmadi."

existing_files="$(docker run --rm -v "${DOC_VOLUME}:/documents:ro" postgres:18-alpine find /documents -type f | wc -l | tr -d '[:space:]')"
[ "$existing_files" = "0" ] || die "Evrak diskinde zaten dosya var. Yukleme durdu."

echo "Imajlar yukleniyor..."
for name in sayacokuma-api sayacokuma-web sayacokuma-db-backup; do
  docker load -i "${BUNDLE}/images/${name}.tar"
done

echo "Veritabani yukleniyor..."
gunzip -c "${BUNDLE}/database.sql.gz" | docker exec -i "$DB_CONTAINER" \
  sh -c 'export PGPASSWORD="$POSTGRES_PASSWORD"; exec psql -v ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"'

echo "Evraklar aciliyor..."
docker run --rm \
  -v "${DOC_VOLUME}:/documents" \
  -v "${BUNDLE}:/bundle:ro" \
  postgres:18-alpine \
  tar -xzf /bundle/documents.tar.gz -C /documents

got_apartments="$(sql_count 'SELECT COUNT(*) FROM "Apartments"')"
got_payments="$(sql_count 'SELECT COUNT(*) FROM "Payments"')"
got_documents="$(sql_count 'SELECT COUNT(*) FROM "ResidentDocuments"')"
got_files="$(docker run --rm -v "${DOC_VOLUME}:/documents:ro" postgres:18-alpine find /documents -type f | wc -l | tr -d '[:space:]')"
got_bytes="$(docker run --rm -v "${DOC_VOLUME}:/documents:ro" postgres:18-alpine sh -c 'sum=0; for f in $(find /documents -type f); do c=$(wc -c < "$f"); sum=$((sum + c)); done; echo "$sum"' | tr -d '[:space:]')"

mismatch=0
[ "$got_apartments" = "$expected_apartments" ] || mismatch=1
[ "$got_payments" = "$expected_payments" ] || mismatch=1
[ "$got_documents" = "$expected_documents" ] || mismatch=1
[ "$got_files" = "$expected_files" ] || mismatch=1
[ "$got_bytes" = "$expected_bytes" ] || mismatch=1
if [ "$mismatch" -ne 0 ]; then
  echo "HATA: Sayilar tutmadi." >&2
  echo "Beklenen daire=${expected_apartments} tahsilat=${expected_payments} evrak=${expected_documents} dosya=${expected_files} bayt=${expected_bytes}" >&2
  echo "Gelen    daire=${got_apartments} tahsilat=${got_payments} evrak=${got_documents} dosya=${got_files} bayt=${got_bytes}" >&2
  die "API acilmadi. Trafigi cevirmeyin."
fi

signing_key="$(grep -E '^AUTH_SIGNING_KEY=' "${BUNDLE}/secrets.env" | tail -n 1)"
signing_key="${signing_key#AUTH_SIGNING_KEY=}"
api_key="$(grep -E '^SECURITY_API_KEY=' "${BUNDLE}/secrets.env" | tail -n 1)"
api_key="${api_key#SECURITY_API_KEY=}"
[ -n "$signing_key" ] || die "Paket imza anahtari bos."
[ -n "$api_key" ] || die "Paket API anahtari bos."

update_env() {
  local key="$1"
  local value="$2"
  local tmp
  tmp="$(mktemp)"
  awk -v k="$key" -v v="$value" '
    BEGIN { found = 0 }
    index($0, k "=") == 1 { print k "=" v; found = 1; next }
    { print }
    END { if (found == 0) print k "=" v }
  ' "$ENV_FILE" > "$tmp"
  cat "$tmp" > "$ENV_FILE"
  rm -f "$tmp"
  chmod 600 "$ENV_FILE"
}

update_env AUTH_SIGNING_KEY "$signing_key"
update_env SECURITY_API_KEY "$api_key"
unset signing_key api_key

echo "Yukleme tamam. Sayilar tuttu."
echo "Daire: ${got_apartments}  Tahsilat: ${got_payments}  Evrak kaydi: ${got_documents}  Evrak dosyasi: ${got_files}"
echo "Imza anahtari ve API anahtari kaynaktan yazildi. Veritabani sifresi degismedi."
echo "API ve web hala kapali. DNS degismedi."
echo "Trafigi cevirmeye hazir olunca: docker compose --env-file ${ENV_FILE} -f ${COMPOSE} up -d"
