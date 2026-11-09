#!/bin/sh
# Cron: her gece 03:00 (Europe/Istanbul) yedek al.
set -eu

CRON_SCHEDULE="${BACKUP_CRON:-0 3 * * *}"
echo "${CRON_SCHEDULE} /scripts/pg-backup.sh >> /var/log/backup.log 2>&1" > /etc/crontabs/root

echo "[$(date -Iseconds)] db-backup cron hazir: '${CRON_SCHEDULE}' (TZ=${TZ:-UTC})"
# İlk açılışta isteğe bağlı anında yedek (manuel test dışında kapalı)
if [ "${BACKUP_RUN_ON_START:-0}" = "1" ]; then
  /scripts/pg-backup.sh || true
fi

exec crond -f -l 8
