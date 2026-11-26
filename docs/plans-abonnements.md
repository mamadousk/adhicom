# Plans & abonnements Adhicom

Tarifs **gérés en base** (`Plans` + `PlanFeatures`), jamais en dur dans le code ; modifiables par l'admin (`/admin/plans`, journalisé dans `AuditLogs`).

## Les 4 plans (valeurs initiales de seed)

| Plan | Prix (indicatif, configurable) | Boutiques | Produits | Stockage | Exports | Rapports avancés | Thèmes premium |
|---|---|---|---|---|---|---|---|
| **Gratuit** | 0 $/mois | 1 | 20 | 100 Mo | Non | Non | Non |
| **Starter** | 9 $/mois | 1 | 200 | 2 Go | Oui | Non | Non |
| **Pro** | 29 $/mois | 3 | 2 000 | 10 Go | Oui | Oui | Oui |
| **Business** | 79 $/mois | 10 | illimité* | 50 Go | Oui | Oui | Oui |

\* limite technique haute, toujours contrôlée côté serveur.
Les valeurs exactes (prix, limites, drapeaux) sont celles de la base au moment T — seule référence faisant foi.

## Cycle de vie

```
Actif ──(échéance + échec paiement)──► Grâce (N jours, paramétrable via subscription.grace_days)
Grâce ──(régularisation)──► Actif
Grâce ──(expiration)──► PastDue  (fonctionnalités limitées : création produits/boutiques bloquée)
PastDue / Actif ──(demande)──► Annulé  (données conservées, boutique repasse limitée/Gratuit)
```

**Expiration ≠ suppression** : les comptes, boutiques, produits, commandes et historiques ne sont jamais supprimés automatiquement. Seules les fonctionnalités sont limitées. Réactivation possible à tout moment (paiement de la période ou prolongation admin).

## Côté commerçant

- `/app/subscription` : plan courant, utilisation vs limites (barres de progression), jours restants, badges d'état (`Active`, `Période de grâce`, `Impayée`), demande de changement de plan (ticket support prérempli — facturation par l'équipe, aucun débit automatique).
- Expiration imminente : notification in-app (templates) + e-mail.
- Dépassement de limite : création bloquée avec message clair + lien « Gérer mon abonnement ».

## Côté administration (`/admin/subscriptions`)

- Prolonger (+30 j), changer de plan, annuler — chaque action tracée dans `SubscriptionHistories` et `AuditLogs`.
- Vue filtrable par statut ; alertes « en grâce » sur le dashboard admin.
- Abonnements en grâce relancés automatiquement par `SubscriptionMaintenanceHostedService` (notification + passage PastDue à l'échéance de grâce).

## Contrôles techniques

- Limites vérifiées **serveur exclusivement** (`ISubscriptionLimitService`) : nombre de boutiques, produits actifs, exports clients (CSV), rapports avancés, thèmes premium.
- Aucun contrôle « client-side only » ; l'UI reflète simplement la réponse du serveur.
- Le plan Gratuit est attribué par défaut à toute inscription ; aucun plan payant n'est requis pour publier une boutique.
