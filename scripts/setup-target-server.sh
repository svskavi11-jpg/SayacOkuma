#!/usr/bin/env bash
# Hedef Ubuntu sunucusunu Westalife tasimasina hazirlar.
# Docker kurar, /opt/westalife altina compose ve .env koyar, yalniz bos PostgreSQL acar.
# API ve web bu adimda baslamaz.
#
# Calistirma (deponun icinden):
#   sudo bash scripts/setup-target-server.sh
#
# .env varsa hicbir seye dokunmadan cikar. Docker kuruluysa guncellemez.
# Web portu 80 doluysa 8088, o da doluysa 8089 secilir. PostgreSQL disari acilmaz.
set -euo pipefail

DEST=/opt/westalife
COMPOSE_PROJECT=sayacokuma-prod
PG_VOLUME="${COMPOSE_PROJECT}_sayacokuma_pgdata_prod"
DB_NAME=sayacokuma
DB_USER=sayacokuma

die() {
  echo "HATA: $*" >&2
  exit 1
}

if [ "$(id -u)" -ne 0 ]; then
  die "Root olarak calistirin: sudo bash scripts/setup-target-server.sh"
fi

if [ ! -r /etc/os-release ] || ! grep -q '^ID=ubuntu$' /etc/os-release; then
  die "Bu script yalniz Ubuntu icindir."
fi

if [ -f "${DEST}/.env" ]; then
  echo "Bu sunucuda Westalife kurulumu var. .env duruyor. Hicbir sey degistirilmedi."
  exit 0
fi

port_busy() {
  local port="$1"
  command -v ss >/dev/null 2>&1 || die "ss komutu yok. Web portu denetlenemedi."
  ss -ltnH | awk '{print $4}' | grep -Eq ":${port}$"
}

choose_web_port() {
  local candidate
  for candidate in 80 8088 8089; do
    if port_busy "$candidate"; then
      echo "Port ${candidate} dolu, atlaniyor." >&2
      continue
    fi
    printf '%s' "$candidate"
    return 0
  done
  return 1
}

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_SRC="${ROOT}/docker-compose.prod.yml"
[ -f "$COMPOSE_SRC" ] || die "docker-compose.prod.yml bulunamadi. Scripti deponun icinden calistirin."

export DEBIAN_FRONTEND=noninteractive

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker kuruluyor..."
  apt-get update
  apt-get install -y ca-certificates curl openssl docker.io docker-compose-v2
  systemctl enable --now docker
else
  echo "Docker kurulu. Guncelleme yapilmadi. $(docker --version)"
fi

if ! command -v openssl >/dev/null 2>&1; then
  apt-get update
  apt-get install -y openssl
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Compose eklentisi kuruluyor..."
  apt-get update
  apt-get install -y docker-compose-v2
fi

docker compose version >/dev/null 2>&1 || die "docker compose komutu yok."

ready=0
for _ in $(seq 1 30); do
  if docker info >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 2
done
[ "$ready" -eq 1 ] || die "Docker hazir olmadi."

install -d -m 0755 "$DEST"
install -m 0644 "$COMPOSE_SRC" "${DEST}/docker-compose.prod.yml"

volume_exists=0
if docker volume inspect "$PG_VOLUME" >/dev/null 2>&1; then
  volume_exists=1
fi

if [ "$volume_exists" -eq 1 ] && [ ! -f "${DEST}/.env" ]; then
  die "Veritabani diski var ama ${DEST}/.env yok. Yeni sifre uretilmedi. Eski .env dosyasini yerine koyun."
fi

if [ ! -f "${DEST}/.env" ]; then
  echo ".env uretiliyor..."
  db_password="$(openssl rand -hex 24)"
  signing_key="$(openssl rand -base64 48 | tr -d '\n')"
  api_key="$(openssl rand -hex 32)"
  seed_password="Aa1!$(openssl rand -hex 8)"
  web_port="$(choose_web_port)" || die "80, 8088 ve 8089 dolu. Web portu secilemedi."
  echo "Web portu: ${web_port}. PostgreSQL disari acilmadi."
  umask 077
  cat > "${DEST}/.env" <<EOF
POSTGRES_DB=${DB_NAME}
POSTGRES_USER=${DB_USER}
POSTGRES_PASSWORD=${db_password}
DATABASE_CONNECTION="Host=db;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${db_password};Include Error Detail=true"
WEB_PORT=${web_port}
SECURITY_API_KEY=${api_key}
AUTH_SIGNING_KEY="${signing_key}"
AUTH_SEED_PASSWORD="${seed_password}"
BACKUP_RETENTION_DAYS=30
BACKUP_CRON=0 3 * * *
EOF
  chmod 600 "${DEST}/.env"
  unset db_password signing_key api_key seed_password web_port
  echo ".env yazildi. Icerigi terminale basilmadi. Dosyayi parola yoneticisine alin."
fi

echo "Bos PostgreSQL aciliyor..."
docker compose --env-file "${DEST}/.env" -f "${DEST}/docker-compose.prod.yml" up -d db

healthy=0
for _ in $(seq 1 30); do
  status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sayacokuma-prod-db 2>/dev/null || true)"
  if [ "$status" = "healthy" ]; then
    healthy=1
    break
  fi
  sleep 2
done
[ "$healthy" -eq 1 ] || die "Veritabani saglikli olmadi."

echo "Hedef hazir. Veritabani saglikli. API ve web kapali."
echo ".env: ${DEST}/.env"
echo "Paket gelince: sudo bash scripts/import-to-target.sh /yol/paket"
