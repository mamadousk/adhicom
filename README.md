# Adhicom

**Une plateforme. Votre boutique. Votre commerce.**

**Adhicom** est la plateforme SaaS multi-boutiques e‑commerce d'**Adhil &amp; Co** pour les commerçants de RDC et d'Afrique francophone — créez votre boutique en quelques minutes, vendez en ligne, encaissez comme vous l'entendez (paiement à la livraison, paiement manuel, ou passerelles Mobile Money).

- Site : `adhicom.com` — application commerçant : `app.adhicom.com` — administration : `admin.adhicom.com` — boutiques publiques : `{votre-nom}.adhicom.com`
- Devises : USD, CDF, XAF, XOF · Dates UTC · français v1
- Plans : Gratuit · Starter · Pro · Business (tarifs gérés en base, jamais en dur)

## Stack

C# · **ASP.NET Core 10** (MVC + Razor + HTMX léger) · Tailwind CSS · PostgreSQL · EF Core (migrations, filtres tenant, contraintes, transactions) · ASP.NET Core Identity · Docker · Nginx · Cloudflare · xUnit — **monolithe modulaire** (jamais de microservices).

## Démarrage rapide (local)

Prérequis : SDK .NET ≥ 10, PostgreSQL ≥ 15 (ou Docker), accès à la boutique en sous-domaine local (`{slug}.localhost` — fonctionne tel quel).

```bash
# 1. Base (Docker ou PostgreSQL local)
docker run --name adhicom-pg -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=adhicom_dev -p 5432:5432 -d postgres:17-alpine

# 2. Lancer (le seed crée rôles, plans, paramètres et le compte admin initial)
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/Adhicom.Web
# → http://localhost:5000  (ou le port affiché)
```

Comptes initiaux (environnement Development, seed automatique) :

| Rôle | Identifiant | Mot de passe | Espace |
|---|---|---|---|
| SuperAdmin | `admin@adhicom.com` (voir `AdminSeed`) | `Admin123!` (à changer) | `/admin` |

Boutique de démonstration « Fashion House » incluse uniquement si `Seeds:DemoData=true` (défaut en Development).

Avec Docker Compose (pile complète) : voir `deployment/docker-compose.yml` et `docs/deploiement.md`.

## Structure de la solution

```
Adhicom.sln
src/
  Adhicom.Domain/          Entités, énumérations, machine à états des commandes
  Adhicom.Application/     Contrats : services, DTO, résultats opérationnels
  Adhicom.Infrastructure/  EF Core (AdhicomDbContext), Identity, paiements, e-mail, fichiers, seed
  Adhicom.Web/             MVC + vues Razor + assets statiques
    Areas/App/             Espace commerçant (rôle Merchant)
    Areas/Admin/           Administration générale (rôle SuperAdmin)
    Areas/Storefront/      Boutiques publiques {slug}.adhicom.com
    Controllers/           Site public, compte, webhooks paiement
    Views/ · wwwroot/      Layouts, pages, CSS/JS sans framework, images SVG
tests/
  Adhicom.UnitTests/       Machine à états, numéros de commande, WhatsApp, plans
  Adhicom.IntegrationTests/Surface publique (/, /tarifs, /health, /robots.txt, /sitemap.xml, accès admin)
  Adhicom.SecurityTests/   HMAC webhooks, en-têtes de sécurité
docs/                    Architecture, déploiement, sécurité, paiements, plans, sauvegarde
deployment/              Dockerfile, docker-compose.yml, nginx.adhicom.conf, backup.sh, restore-test.sh
```

## Fonctionnalités (v1)

- **Site principal** : présentation, fonctionnalités, tarifs, FAQ, CGU/confidentialité, contact — SEO (URLs lisibles, `sitemap.xml` dynamique, `robots.txt`, Open Graph, canoniques, JSON-LD, dashboards `noindex`).
- **Comptes** : inscription, connexion, reset par mot de passe — Identity complet.
- **Boutique** : création + sous-domaine réservé unique + assistant 8 étapes (logo, bannière, thème/couleurs, catégories, produits, paiements, livraisons, publication) + barre de progression.
- **Catalogue** : produits (CRUD, photos multiples, prix barré, SKU, variantes), catégories, stock simple avec **alerte de seuil**, **anti double décrément** (décrément atomique SQL + historique `InventoryMovement`).
- **Panier & commande publique** : nom, téléphone, e-mail (opt.), ville, commune, adresse, instructions — numéro public `AHC-YYYYMMDD-XXXXX` — e-mail de confirmation.
- **Cycle de vie exact** : `En attente → Confirmée → En préparation → Prête → En livraison → Livrée` + `Annulée / Refusée / Retournée` — chaque transition historisée (auteur, date, commentaire).
- **Paiements** : COD · manuel (validés commerçant) · passerelles préparées (Moko Afrika, Nomba, OPTGateway, Malipo) — webhooks HMAC + idempotence + montants re-vérifiés serveur ; **paiements versés directement au commerçant** (jamais collectés par Adhicom).
- **Livraison** : modes paramétrables (forfait / gratuit / retrait / à négocier), gratuité au-delà d'un seuil, délais affichés, instructions publiques.
- **Clients** : base clients isolée par boutique, compteurs, historique, export CSV (plan requis).
- **Abonnements** : 4 plans seedés · limites serveur · grâce paramétrable · expiration ≠ suppression.
- **Admin** : dashboard plateforme, boutiques (suspendre/réactiver/fermer, motif), utilisateurs, plans (CRUD tarifs), abonnements (prolonger / changer de plan / annuler), signalements, support, **journal d'audit**, paramètres globaux, contenu éditorial.
- **Notifications** : in-app (commandes, stock faible, abonnement) + e-mails transactionnels.

## Sécurité (résumé)

Isolation multi-tenant par filtre EF Core global résolu **serveur** ; HTTPS/HSTS ; CSP stricte ; anti-forgery ; rate limiting ; secrets par variables d'environnement ; uploads contrôlés ; audit admin ; webhooks HMAC-SHA256 ; mots de passe hachés Identity. Détails : `docs/securite.md`.

## Base de données & migrations

Le démarrage applique les migrations EF Core si présentes, sinon crée le schéma (`EnsureCreated`) — pour un flux de production propre :

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/Adhicom.Infrastructure --startup-project src/Adhicom.Web
```

## Sauvegarde

`deployment/backup.sh` (quotidien) + `deployment/restore-test.sh` (mensuel — restaure dans une base jetable, vérifie, supprime). Voir `docs/sauvegarde-restauration.md`.

## Tests

```bash
dotnet test Adhicom.sln                 # unitaires (domaine) + sécurité HMAC
ADHICOM_TEST_DB=1 dotnet test           # + intégration HTTP (nécessite PostgreSQL)
```

## Licence & identité

Propriétaire — © Adhil & Co (Kinshasa). « Adhil & Co » désigne la société mère, « Adhicom » la plateforme. Crédits : équipe Adhicom.
