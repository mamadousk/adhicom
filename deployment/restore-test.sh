#!/usr/bin/env bash
# =====================================================
# Adhicom — TEST de restauration (à exécuter au moins 1×/mois).
# Restaure un dump dans une base jetable, vérifie l'intégrité, puis la supprime.
# Usage : ./deployment/restore-test.sh deployment/backups/adhicom-YYYYMMDD-HHMMSS.dump
# =====================================================
set -euo pipefail

DUMP="${1:?Chemin du fichier .dump requis}"
TEST_DB="adhicom_restore_test_$$"
DB_USER="${POSTGRES_USER:-adhicom}"
DB_HOST="${POSTGRES_HOST:-127.0.0.1}"
DB_PORT="${POSTGRES_PORT:-5432}"

echo "[restore-test] Création de la base jetable $TEST_DB"
PGPASSWORD="${POSTGRES_PASSWORD:?POSTGRES_PASSWORD requis}" \
  createdb --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" "$TEST_DB"

trap 'echo "[restore-test] Suppression de $TEST_DB"; PGPASSWORD="$POSTGRES_PASSWORD" dropdb --if-exists --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" "$TEST_DB"' EXIT

echo "[restore-test] Restauration de $DUMP"
PGPASSWORD="$POSTGRES_PASSWORD" \
  pg_restore --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" \
             --dbname="$TEST_DB" --no-owner --no-privileges "$DUMP"

echo "[restore-test] Vérifications d'intégrité"
PGPASSWORD="$POSTGRES_PASSWORD" psql --host="$DB_HOST" --port="$DB_PORT" --username="$DB_USER" --dbname="$TEST_DB" -c \
  "SELECT 'stores' AS table, COUNT(*) FROM \"Stores\"
   UNION ALL SELECT 'products', COUNT(*) FROM \"Products\"
   UNION ALL SELECT 'orders', COUNT(*) FROM \"Orders\"
   UNION ALL SELECT 'plans', COUNT(*) FROM \"Plans\";"

echo "[restore-test] OK — sauvegarde restaurable."
