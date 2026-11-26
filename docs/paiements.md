# Paiements Adhicom

Deux flux **strictement séparés** :

| Flux | Parties | Mécanisme | Statut v1 |
|---|---|---|---|
| **Abonnements** (commerçant → Adhil & Co) | Facturation SaaS | Facture + Mobile Money / virement, traitée par l'équipe (demande de plan dans l'app) | Manuel, audité |
| **Ventes** (client final → commerçant) | Boutique publique | COD, manuel (preuve + validation commerçant) ; passerelles préparées | COD + manuel OD opérationnels |

Adhicom ne collecte pas les fonds des marchands et n'agit pas comme agrégateur de paiement.

## Moyens côté boutique (StorePaymentConfiguration)

- **cod** — Paiement à la livraison (validation à la remise).
- **manual** — Le client paie hors ligne (Mobile Money / virement) et envoie la preuve ; le commerçant valide dans l'app (« Paiement confirmé ») → commande marquée payée.
- **bank_transfer** — Instructions de virement affichées au client ; confirmation manuelle.
- **mobile_money** / **card** — Passerelles automatisées ; **désactivées par défaut**, activables après contrat + KYC.

Chaque boutique configure instructions publiques (numéro/compte marchand) et code identifiant. Le panier et le tunnel n'affichent que les modes activés par la boutique ; le serveur re-valide le choix à la commande.

## Passerelles préparées (IPaymentProvider)

`Moko Afrika`, `Nomba`, `OPTGateway`, `Malipo` — implémentation commune `HostedCheckoutProvider` :

- **Init** : création de session de paiement (clé API + URL de base configurées côté serveur ; jamais exposées).
- **Webhook** : `POST /webhooks/payments/{provider}` — signature **HMAC-SHA256** (hex ou base64, préfixe `sha256=` toléré), comparaison en temps constant, secret stocké en variable d'environnement.
- **Idempotence** : événement journalisé dans `PaymentWebhookEvents` avec index unique `(Provider, EventId)` ; rejeux ignorés.
- **Montants** : le montant/devise attendus sont **re-lus en base** avant marquage payé — un webhook ne crée pas d'argent.
- **Fonds** : versés **directement au commerçant** (comptes sous-marchand KYC chez le prestataire) — pas de collecte Adhicom.

Activation progressive recommandée : COD + manuel d'abord ; passerelles en pilote avec 2–3 boutiques, puis généralisation.

## Cycle de vie du paiement d'une commande

```
Commande créée
  ├─ COD     → PaymentStatus.Unpaid    (encaissé à la remise, puis validé)
  ├─ manuel  → PaymentStatus.ToBeConfirmed (client paie + preuve → commerçant valide)
  └─ passerelles → PaymentStatus.Pending (webhook → Paid / Failed)
```

Le tableau de bord affiche CA **encaissé** uniquement (`Paid` ou `Delivered`), jamais les commandes non payées.

## Abonnements (côté Adhil & Co)

- Plans `Gratuit / Starter / Pro / Business` — tarifs en base, jamais en dur ; modifiables par l'admin (`/admin/plans`, audité).
- Demande de changement de plan dans l'app → ticket support prérempli ; l'équipe facture (Mobile Money / virement) et prolonge/change le plan (`/admin/subscriptions`).
- Grâce paramétrable (`SystemSetting: subscription.grace_days`, défaut 7) → `Grace` → `PastDue` (fonctionnalités limitées, données conservées) → relance ; **jamais de suppression**.
- `SubscriptionHistory` + `SubscriptionPayment` tracent chaque événement.

## Opérations

- Confirmer une réception : commande → « Confirmer le paiement reçu » (+ référence optionnelle).
- Litige manuel : conserver la preuve, noter dans le commentaire de transition de statut ; escalade vers `support@adhicom.com`.
- Rotation de secrets webhook : variable d'environnement du prestataire → redéploiement ; anciennes signatures rejetées immédiatement.
