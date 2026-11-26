# Sauvegarde & restauration

Principe : **une sauvegarde qui n'a jamais été restaurée n'est pas une sauvegarde.**

## Quoi sauvegarder

1. **Base PostgreSQL** — la source de vérité (boutiques, produits, commandes, abonnements, audit…)
2. **Uploads** (`/app/wwwroot/uploads`) — logos, bannières, photos produits
3. **Configuration** — `appsettings.*.json` (sans secrets) + inventaire des variables d'environnement (le coffre de secrets reste la référence)

## Sauvegarde quotidienne

`deployment/backup.sh` — à 03:00 UTC :

```bash
POSTGRES_PASSWORD=... POSTGRES_USER=adhicom POSTGRES_HOST=127.0.0.1 ./deployment/backup.sh
```

Produit dans `deployment/backups/` :
- `adhicom-YYYYMMDD-HHMMSS.dump` (format custom PostgreSQL, compressé)
- `uploads-YYYYMMDD-HHMMSS.tar.gz` (si répertoire présent)
Rétention 14 jours locale ; **archiver hors-site** (S3 autre région) via rclone/rsync chiffré.

Crontab exemple :

```
0 3 * * * /opt/adhicom/deployment/backup.sh >> /var/log/adhicom-backup.log 2>&1
```

## Test de restauration mensuel (obligatoire)

```bash
POSTGRES_PASSWORD=... ./deployment/restore-test.sh deployment/backups/adhicom-20261126-030000.dump
```

Le script : crée une base jetable `adhicom_restore_test_*`, y restaure le dump, vérifie `Stores` / `Products` / `Orders` / `Plans`, puis **supprime** la base jetable (EXIT trap). À exécuter au moins une fois par mois et avant chaque mise à jour majeure.

## Restauration de production (incident)

```bash
# 1. Arrêter l'application
docker compose -f deployment/docker-compose.yml stop web

# 2. Sauvegarder l'état actuel (même corrompu — preuve)
pg_dump --format=custom -h 127.0.0.1 -U adhicom adhicom > pre-restore-$(date -u +%Y%m%d-%H%M%S).dump

# 3. Restaurer
pg_restore --clean --if-exists --no-owner --no-privileges -h 127.0.0.1 -U adhicom -d adhicom adhicom-AAAAMMJJ-HHMMSS.dump

# 4. Uploads si nécessaire
tar -xzf uploads-AAAAMMJJ-HHMMSS.tar.gz -C /app/wwwroot/

# 5. Redémarrer et vérifier
docker compose -f deployment/docker-compose.yml up -d web
curl -fsS https://adhicom.com/health
```

Durée cible (RTO) : < 1 h ; perte maximale admissible (RPO) : < 24 h (sauvegarde quotidienne) — ajustable à 1 h avec archivage WAL.

## Vérifications après restauration

- `/health` = 200 ; site public affiché ; `/app` accessible avec un compte connu
- Commandes du jour présentes ; un produit au hasard ; un abonnement en grâce connu
- Journal d'audit (`/admin/audit`) — intégrité des entrées
- Journaux applicatifs : pas d'erreurs de migration/EF

## Conservation & conformité

- 30 jours glissants minimum recommandés (7 dumps hebdomadaires + 30 quotidiens hors-site)
- Dumps chiffrés au repos (S3 SSE ou age/gpg avant transfert)
- Accès aux sauvegardes : l'équipe Adhil & Co uniquement ; registre d'accès conseillé
