# Sécurité Adhicom

Principes : moindre privilège, défense en profondeur, jamais de confiance côté client.

## Authentification & identité

- ASP.NET Core Identity : hachage des mots de passe (PBKDF2), verrouillage après tentatives, confirmation d'e-mail et reset par token à usage unique.
- Rôles : `SuperAdmin` (équipe Adhil & Co) et `Merchant` (commerçants). `/admin/**` exige `SuperAdmin` **et** le filtre tenant global y est contourné explicitement (portée plateforme).
- Sessions via cookies Identity `HttpOnly`, `Secure` en production, `SameSite=Lax`.
- Mots de passe : politique Identity par défaut renforcée (longueur ≥ 8, diversité) ; le mot de passe admin seedé doit être changé au premier login.

## Multi-tenant

- Filtre EF Core global par requête sur toutes les entités de boutique ; contournement seulement via `ITenantContext.BypassScope()` (seed, maintenance, webhooks, admin) — traçable et revu.
- La boutique active du commerçant est dérivée de `StoreMember` (base), **jamais** d'un paramètre client ; revalidée à chaque requête.
- Boutiques publiques : résolution par slug/hôte ; les commandes n'accèdent qu'aux données de la boutique résolue.
- Identifiants non séquentiels (GUID) — pas d'énumération d'objets par ID.

## Entrées & sorties

- Anti-forgery (token) obligatoire sur tous les POST (attribut global `AutoValidateAntiforgeryToken`).
- Validation modèle (DataAnnotations) côté serveur ; Razor encode les sorties par défaut (HTML).
- Uploads : extensions/MIME contrôlés, taille limitée (4 Mo), stockage hors code (`/uploads`), nom de fichier régénéré.
- SQL : EF Core paramétré partout ; pas de SQL brut sauf liste blanche (décrément atomique conditionnel) — revu.

## Web & transport

- HTTPS forcé, HSTS (max-age 2 ans, preload), TLS ≥ 1.2 côté Nginx/Cloudflare.
- En-têtes : `Content-Security-Policy` stricte (`default-src 'self'`, `img-src 'self' data: https:`, `script-src 'self'`, `frame-ancestors 'self'`), `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` restreinte.
- Rate limiting applicatif (fixed window) sur login / inscription / reset / commandes (voir Program.cs) + Cloudflare WAF en amont.
- Secrets : jamais dans le dépôt (`.env` ignoré, revue `git secrets`/GitHub push protection conseillée) ; comparaison de signatures HMAC en temps constant.

## Webhooks de paiement

- Signature HMAC-SHA256 vérifiée (hex ou base64) avec secret par prestataire ; rejet si absente/invalide.
- **Idempotence** : `PaymentWebhookEvents` déduplique par `(Provider, EventId)`.
- Montants et devises **re-vérifiés côté serveur** avant tout marquage « payé » ; le webhook ne fait jamais confiance au corps brut.
- Taille max 256 Ko ; content-type JSON ; journalisation sans données sensibles.

## Audit & conformité

- `AuditLogs` : actions sensibles admin (suspension boutique, changement de plan/abonnement, paramètres globaux, contenus) avec auteur, cible, IP et horodatage UTC.
- CGU + politique de confidentialité publiques (`/cgu`, `/confidentialite`) ; données personnelles traitées selon finalités (commande, livraison) ; pas de revente.
- RGPD-friendly : suppression logique (jamais de hard delete), export CSV client réservé au plan autorisé, droit d'opposition via support.
- En-têtes `Permissions-Policy` : caméra/micro/géolocalisation désactivés.

## Pratiques d'exploitation

- Conteneur non-root ; base en rôle dédié non-superuser ; PostgreSQL non exposé publiquement (`127.0.0.1`).
- Sauvegardes quotidiennes + test de restauration mensuel (`deployment/backup.sh`, `restore-test.sh`).
- Mises à jour de dépendances : revue trimestrielle ; CI exécute build + tests.
- Journalisation sans PII dans les logs applicatifs (noms/téléphones masqués côté support quand possible).

## Limites connues v1

- 2FA non implémentée (prévue pour l'espace admin).
- WAF applicatif fourni par Cloudflare (règles managées recommandées).
- Signature webhook : certains prestataires exigent un schéma maison — adaptateur prêt (`HostedCheckoutProvider.VerifyWebhookSignature`).
