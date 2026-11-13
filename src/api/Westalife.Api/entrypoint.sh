#!/bin/sh
# Log volume'u Docker root olarak olusturur. API imaji normalde app kullanicisidir.
# Compose api servisi root baslatir; bu betik dizinin sahibini app yapar ve sureci app'e birakir.
# Migrator ayni imaji kendi entrypoint'i ile calistirdigi icin bu betige girmez.
set -eu
if [ "$(id -u)" -eq 0 ]; then
  mkdir -p /app/logs
  chown app:app /app/logs
  exec setpriv --reuid=app --regid=app --init-groups --inh-caps=-all dotnet Westalife.Api.dll
fi
exec dotnet Westalife.Api.dll
