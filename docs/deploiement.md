# Déploiement Adhicom (production)

Cible recommandée : **VPS (Ubuntu) + Docker + Nginx + PostgreSQL + Cloudflare** devant les domaines `*.adhicom.com`.

## 1. Prérequis

- Docker Engine ≥ 24 + compose plugin
- PostgreSQL 17 (service compose, managé, ou serveur dédié)
- Nginx (ou Cloudflare Tunnel / load balancer) — voir `deployment/nginx.adhicom.conf`
- Certificat TLS wildcard (`*.adhicom.com`) via DNS-01 (certbot + Cloudflare) ou Cloudflare Full (strict)
- Sauvegardes : script `deployment/backup.sh` + test mensuel de restauration

## 2. Déploiement avec le compose

```bash
cp .env.example .env   # créer et remplir (jamais commité)
docker compose -f deployment/docker-compose.yml up -d --build
docker compose -f deployment/docker-compose.yml logs -f web
```

Variables d'environnement attendues : `POSTGRES_PASSWORD`, `ADMIN_PASSWORD`, `ADMIN_EMAIL`, `BASE_DOMAIN`, `SMTP_*`, `MOKO_SECRET`/`NOMBA_SECRET`/`OPTGATEWAY_SECRET`/`MALIPO_SECRET` (optionnels tant que les passerelles ne sont pas contractualisées).

## 3. Premier démarrage

Au boot, l'application applique les migrations EF Core (ou `EnsureCreated` si aucune migration n'est encore générée) puis exécute `DatabaseSeeder` : rôles (`SuperAdmin`, `Merchant`), 4 plans tarifaires, fournisseurs de paiement, paramètres et templates de notifications, compte admin initial (`AdminSeed:Email` / `AdminSeed:Password`).

> **Générez les migrations une fois** avec le SDK .NET ≥ 10 :
> `dotnet ef migrations add InitialCreate --project src/Adhicom.Infrastructure --startup-project src/Adhicom.Web`
> puis rebuilder. Sans migrations, `EnsureCreated` crée le schéma — suffisant pour démarrer, mais sans historique.

## 4. Hôtes & routage

| Hôte | Zone |
|---|---|
| `adhicom.com` / `www` | Site public (marketing, tarifs, FAQ, CGU) |
| `app.adhicom.com` | Espace commerçant (dashboard, produits, commandes, paramètres) |
| `admin.adhicom.com` | Administration générale (équipe Adhil & Co, rôle SuperAdmin) |
| `{slug}.adhicom.com` | Boutique publique du commerçant (catalogue, panier, commande) |

Le routage inter-hôtes est assuré par `HostResolutionMiddleware` (réécriture interne : `app.` → `/app`, `admin.` → `/admin`, `{slug}.` → `/storefront/{slug}`) avec hôtes statiques inchangés. Nginx transmet `Host` tel quel ; TLS met fin chez Nginx/Cloudflare ; HSTS est émis par l'app et par Nginx.

## 5. Cloudflare

- Proxy **ON** (orange cloud) pour tous les hôtes ; mode SSL/TLS *Full (strict)*
- Règle de page : cache long pour `/css`, `/js`, `/img` ; **Bypass** pour `/app`, `/admin`, `/account`, `/webhooks`
- Rate limiting Cloudflare en complément du rate limiting applicatif
- Enregistrements : `A/AAAA adhicom.com`, `A/AAAA *.adhicom.com`, `CNAME www → adhicom.com` (ou A direct)

## 6. E-mails transactionnels

SMTP générique (`Smtp:Host/Port/User/Password/From`). Sans configuration, les e-mails sont journalisés (mode silencieux) et le lien de réinitialisation est affiché en développement.

Modèles utilisés : confirmation de commande client, notifications commerçant (nouvelle commande), e-mails d'identité (reset). Templates gérables par l'admin (table `NotificationTemplates`).

## 7. Santé & exploitation

- Sonde : `GET /health` (200 JSON)
- Logs : stdout conteneur → collecteur (Docker logs, journald, Loki…)
- Sauvegarde quotidienne 03:00 UTC + `restore-test.sh` mensuel (restauration dans base jetable)
- Mises à jour : rebuild image → redémarrage sans perte (migrations idempotentes via EF)

## 8. Sécurité production

Voir `docs/securite.md`. En résumé : secrets via env, poste d'admin derrière SSO éventuel + mot de passe fort (`AdminSeed:Password` à changer ensuite), en-têtes durcis, CSP stricte, audit permanent, principe du moindre privilège sur la base (rôle dédié, pas superuser).
