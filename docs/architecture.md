# Architecture Adhicom

**Adhil & Co** · Plateforme e-commerce multi-boutiques pour commerçants de RDC / Afrique francophone.

## Vue d'ensemble

Monolithe modulaire **unique en ASP.NET Core 10** — jamais de microservices. Un seul processus sert les quatre espaces (site, app commerçant, boutiques publiques, admin générale), la séparation se fait par hôtes et par zone (Areas).

```
                        ┌────────────────────────────────────────┐
                        │      ASP.NET Core 10 (Adhicom.Web)     │
                        │  MVC + Razor + HTMX léger + Tailwind   │
                        │                                        │
   Adhicom.Domain ◄─────┤  Zones :                                │
   Entités, règles,     │   /                 site public         │
   machine à états      │   /app             app commerçant      │
                        │   /storefront/{slug} boutiques publiques│
   Adhicom.Application  │   /admin           administration       │
   Contrats (DTO,       │   /webhooks        paiements (HMAC)    │
   services, résultats)  │                                        │
                        └────────┬───────────────────────────────┘
                                 │
                        Adhicom.Infrastructure
                        EF Core + PostgreSQL, Identity,
                        e-mail, fichiers, providers de paiement
```

## Couches

| Projet | Contenu | Dépendances |
|---|---|---|
| `Adhicom.Domain` | Entités (TenantEntity), énumérations, `OrderStatusMachine`, règles métier | aucune |
| `Adhicom.Application` | Interfaces de services, DTO (`PlaceOrderCommand`…), `OperationResult`, constantes | Domain |
| `Adhicom.Infrastructure` | `AdhicomDbContext` (filtres tenant), Identity, `Ef*Services`, `PaymentProviders`, `DatabaseSeeder` | Application, Domain |
| `Adhicom.Web` | Program.cs, middlewares, contrôleurs (Public/Account/App/Admin/Storefront/Webhooks), vues Razor, assets | Infrastructure |

## Multi-tenant (isolation des données)

- Toute entité de boutique porte `StoreId` (`TenantEntity`).
- Un **filtre global EF Core par requête** (`HasQueryFilter` sur `TenantEntity`) confine chaque requête à la boutique active, résolue **côté serveur uniquement** (jamais depuis l'URL/formulaire pour l'espace commerçant).
- L'espace commerçant détermine la boutique active via `StoreMember` + cookie `adhicom_active_store`, **revalidé à chaque requête** (`MerchantControllerBase`).
- La boutique publique est résolue via le slug/hôte (`StoreDomain`) — `StorefrontResolutionMiddleware` réécrit `{slug}.adhicom.com → /storefront/{slug}`.
- Les opérations système/admin (seed, maintenance d'abonnements, webhooks) ouvrent un **scope contournant le filtre** (`ITenantContext.BypassScope()`), explicitement, jamais par défaut.

## Flux de commande (intégrité)

1. Panier par boutique (`ShoppingCart`), token anonyme en cookie `adhicom_cart_{storeId}`.
2. Commande publique → recalcul **100 % serveur** des prix/frais/remises (jamais de montants côté client).
3. Stock : pré-validation, puis décrément **atomique SQL** (`ExecuteUpdateAsync` conditionnel) — anti double décrément via `Order.StockDeducted` + mouvements `InventoryMovement`.
4. Cycle de vie : `Pending → Confirmed → Preparing → ReadyToShip → OutForDelivery → Delivered`, branches `Cancelled / Refused / Returned` — transitions **historisées** (`OrderStatusHistory`) avec auteur, date, commentaire.
5. Paiement : COD / manuel (validé commerçant). Intégrations passerelles préparées (HMAC, idempotence via `PaymentWebhookEvents`, montants re-vérifiés serveur) — voir `docs/paiements.md`.

## Plans & limites

`Gratuit / Starter / Pro / Business` : tarifs **seedés** (table `Plans`, éditables par l'admin) — jamais en dur. Limites (boutiques, produits, stockage, thèmes, exports, analytiques, utilisateurs) vérifiées **serveur** via `ISubscriptionLimitService`. Expiration : période de grâce paramétrable → `PastDue` (limitation, jamais suppression) ; les données restent toujours.

## Sécurité (résumé)

HTTPS forcé, HSTS, en-têtes durcis (CSP stricte, `X-Frame-Options SAMEORIGIN`), anti-forgery sur tous les POST, Identity (mots de passe hachés), rôles `SuperAdmin`/`Merchant`, rate limiting sur login/inscription/reset (voir `Program.cs`), audit (`AuditLogs`) pour les actions admin, secrets via variables d'environnement (jamais en dépôt), CSP compatible image/hero distants.

## Données & contenus

UTC partout, français v1, devises USD/CDF/XAF/XOF (`StoreSettings.Currency`), slugs uniques réservés (liste de noms interdits), sauvegardes chiffrées conseillées hors-site (`deployment/backup.sh` + `restore-test.sh` mensuel).

## Choix connus / limites v1

- Redis non requis en v1 (cache applicatif simple) — prévu pour sessions distribuées & cache catalogue.
- Paiements automatiques : activés **après validation contractuelle** des prestataires (KYC, webhooks allowlistés). COD et manuel fonctionnent sans.
- Multi-utilisateurs par boutique : rôles fins `StoreMember` préparés, v1 = propriétaire.
- Domaines personnalisés boutique : `StoreDomain.IsCustom` préparé ; activation ultérieure.
