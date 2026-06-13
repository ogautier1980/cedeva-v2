# 0010 — Paiement en ligne : abstraction agnostique du fournisseur, Stripe en premier

**Statut :** Accepté (remplace l'import CODA / rapprochement bancaire, supprimés)

## Contexte
Cedeva facturait des réservations mais encaissait hors-ligne : l'« import CODA » et le
rapprochement bancaire servaient à pointer manuellement les virements. C'était lourd, peu fiable et
voué à la suppression. Le besoin réel : permettre au parent de **payer directement depuis l'iframe
d'inscription publique**, lier le paiement à la réservation et passer automatiquement en « payé ».
Mollie a été envisagé mais aucun compte de test fonctionnel n'a pu être créé ; Stripe oui.

## Décision
Encaisser via **Stripe Checkout (page hébergée)**, mais derrière une **abstraction agnostique** pour
pouvoir changer de fournisseur plus tard :
- `IPaymentGateway` (`CreateCheckoutAsync` + `ParseWebhook`) — implémentation `StripePaymentGateway`
  (Stripe.net, client construit paresseusement pour rester résolvable même non configuré).
- `IBookingPaymentService` / `BookingPaymentService` — applique un webhook payé à la réservation
  (enregistre un `Payment(Online)`, met à jour `PaidAmount` + `PaymentStatus`, **idempotent** sur la
  référence fournisseur).
- `OnlinePaymentController` (`[AllowAnonymous]`) : `Checkout` (redirige vers la page hébergée),
  `Return`, et `Webhook` (signé). Bouton « Payer en ligne » sur la page de confirmation publique
  (`target="_top"` car Stripe Checkout refuse d'être chargé en iframe cross-origin).
- Secrets via configuration : `Stripe:SecretKey` / `Stripe:WebhookSecret` (app settings Azure
  `Stripe__SecretKey` / `Stripe__WebhookSecret`), jamais committés.
- L'import CODA et le rapprochement bancaire (entités, services, vues, migration) sont **supprimés**.

## Conséquences
- Le parent paie en fin d'inscription ; le webhook signé met la réservation à jour de façon idempotente
  (rejouable sans double-comptage).
- Changer de fournisseur = une nouvelle implémentation d'`IPaymentGateway`, sans toucher aux
  contrôleurs ni au domaine.
- La vérification de signature du webhook dépend du `WebhookSecret` : s'il manque, les webhooks sont
  rejetés (pas de mise à jour silencieuse).
- Le montant proposé est le **solde dû** (`TotalAmount - PaidAmount`) ; aucun paiement n'est proposé
  si le solde est nul.

## Alternatives écartées
- **Mollie** : pas de compte de test exploitable au moment du choix.
- **Stripe Elements / paiement intégré** : plus de surface front + conformité PCI à gérer ; Checkout
  hébergé délègue la saisie carte à Stripe.
- **Conserver CODA / rapprochement** : process manuel, fragile, sans valeur pour l'utilisateur final.
