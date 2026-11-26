#!/usr/bin/env bash
# =====================================================
# Adhicom — sauvegarde PostgreSQL + uploads.
# Usage : ./deployment/backup.sh           (écrit dans deployment/backups/)
# Cron  : 0 3 * * * /opt/adhicom/deployment/backup.sh >> /var/log/adhicom-backup.log 2>&1
# Restauration : voir docs/sauvegarde-restauration.md
# =====================================================
set -euo pipefail

STAMP="$(date -u +%Y%m%d-%H%M%S)"
OUT_DIR="${1:-$(cd "$(dirname "$0")" && pwd)/backups}"
mkdir -p "$OUT_DIR"

DB_NAME="${POSTGRES_DB:-adhicom}"
DB_USER="${POSTGRES_USER:-adhicom}"
DB_HOST="${POSTGRES_HOST:-127.0.0.1}"
DB_PORT="${POSTGRES_PORT:-5432}"

echo "[adhicom-backup] $STAMP — dump de la base $DB_NAME@$DB_HOST:$DB_PORT"
PGPASSWORD="${POSTGRES_PASSWORD:?POSTGRES_PASSWORD requis}" \
  pg_dump --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" \
          --format=custom --no-owner --no-privileges \
          --file="$OUT_DIR/adhicom-$STAMP.dump" "$DB_NAME"

# Uploads (logos/bannières/produits) — inclus si présents
if [ -d /app/wwwroot/uploads ]; then
  tar -czf "$OUT_DIR/uploads-$STAMP.tar.gz" -C /app/wwwroot uploads
fi

# Rétention : 14 jours locaux (archiver hors-site ensuite — S3, autre région…)
find "$OUT_DIR" -name 'adhicom-*.dump' -mtime +14 -delete
find "$OUT_DIR" -name 'uploads-*.tar.gz' -mtime +14 -delete

echo "[adhicom-backup] Terminé : $OUT_DIR"
