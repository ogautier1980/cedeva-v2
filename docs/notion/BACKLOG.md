# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel

Source : export Notion du client (captures d'écran annotées) + exemples fournis (`17.pdf` pour le
rapport ONE, `GERARD_MULLER_-_S2.pdf` pour l'attestation mutuelle).

**Statut : tous les lots (A à K) sont livrés et déployés en production.**

## Points d'attention restants

- **Lot H — Attestation fiscale** : aucun exemple réel n'a jamais été fourni. Le document généré
  (`ParentsController.FiscalAttestation`) utilise des mentions légales génériques belges (frais de
  garde d'enfants, art. 113 CIR92) — **à faire relire par Thomas ou un comptable avant tout usage
  réel**. Un avertissement visible (non imprimé) le rappelle sur l'écran.
- **Lot I, étape 4 — `MaxChildrenPerDay`** : appliqué comme un plafond sur le nombre total
  d'inscriptions actives de l'activité, pas un vrai comptage par jour calendaire. Approximation
  pragmatique, à affiner si Thomas la juge insuffisante.
- **Lot K — quota par année de naissance** : une réservation couvre toujours tous les jours actifs
  de l'activité en une fois (pas d'inscription partielle par semaine), donc un quota par (activité,
  année de naissance) se comporte déjà comme un quota par semaine dans le cas courant. À revoir si
  le flux permet un jour une inscription partielle.
- **Lot J — signalétique de l'activité** : les champs (logo, adresse, téléphone, etc.) sont
  persistés et peuvent surcharger ceux de l'organisation, mais rien ne les consomme encore dans un
  document ou e-mail réel — le fallback reste à câbler au moment de l'utiliser.
- **Bancontact** doit être activé côté Dashboard Stripe pour le compte live (paiement en ligne).

## Lot A — Accueil & Navigation

Page d'accueil réduite à une grille de gros boutons par activité (titre + dates), plus une section
« Paramètres généraux » regroupant les liens auparavant dans le menu hamburger (supprimé). Le
tableau de bord d'une activité affiche 8 tuiles (dont « Paramètres »), le menu du haut se limite à
un dropdown « Pages spéciales ».

## Lot B — Confirmation des inscriptions

Le parent ne paie plus à l'inscription : à la confirmation par le coordinateur (`ManageBookings`),
un mail avec lien de paiement Stripe (carte + Bancontact) et QR code est envoyé automatiquement si
un solde reste dû. Le montant total est ajustable manuellement à ce moment-là
(`ConfirmBookingRequest.AdjustedTotalAmount`). Groupe et fiche médicale sont assignables séparément
(`Bookings/Edit`, `GroupAssignment`).

## Lot C — Présences

Fiche enfant enrichie (`Bookings/Details`), colonne « Payé » sur `Presences.cshtml`, listes de
groupes imprimables avec sélection multiple et export PDF/Excel, bouton « Imprimer tous les groupes
du jour », total des présences décomposé par indicateur ONE.

## Lot D — Comptes / Finances

`Transactions.cshtml` simplifié (liste nue). Numéro de ticket unique par ligne, partagé entre
paiements et dépenses, remis à 1 par activité. Écran unique à onglets pour ajouter un paiement ou
une dépense. Catégories de dépenses avec type (Expense/Income/OffBalance) et budget ; les dépenses
« hors bilan » sont exclues des totaux et affichées à part.

## Lot E — E-mails

`SendEmail.cshtml` épuré (panneaux repliables). Trois modèles verrouillés par organisation
(confirmation, rappel fiche médicale, rappel paiement) : non créables/dupliquables/supprimables, ne
sont jamais copiés par activité. Bouton « Envoyer » sur la liste des modèles pré-charge `SendEmail`.

## Lot F — Excursions

Formulaire simplifié (heure/type retirés de l'écran). Destinataire « inscrits à l'activité, pas
encore à cette excursion ». Modèle « Proposition d'excursion » (`EmailTemplateType.ExcursionProposal`)
auto-suggéré au chargement de `Excursions/SendEmail`, avec les variables `%excursion_name%`,
`%excursion_date%`, `%child_firstname%`, `%child_lastname%` (désormais réellement résolues à
l'envoi).

## Lot G — Équipe

Présences équipe jour par jour (`TeamMemberDay`, page `TeamPresences`), utilisées par le calcul
salarial au lieu de supposer 100% des jours de l'activité. Compléments/dépenses par membre déjà
existants. Stockage de l'extrait de casier judiciaire.

## Lot H — ONE et attestations fiscales

- **Rapport ONE** (`ActivityManagement/OneReport`) : listings par tranche d'âge + présences
  hebdomadaires, par activité, calqué sur `17.pdf`.
- **Attestation fiscale** (`ParentsController.FiscalAttestation`) : groupée par association — tous
  les enfants d'un parent, toutes les activités de l'organisation, pour une année donnée. Voir
  avertissement en haut de ce document.

## Lot I — Assistant de création d'activité

`ActivityWizardController` (7 étapes avec jauge de progression) : Titre + Dates, Paramétrage des
dates (ajout de dates, regroupement par semaine), Règlement (lien PDF + case à cocher), Limitations
(codes postaux, quotas), Autres questions, Affichage (fenêtre de publication, redirection), puis
retour à l'écran d'intégration iframe existant. Couverture de tests complète (25 tests).

## Lot J — Paramètres de l'activité

Signalétique de l'activité (logo, titre affiché, adresse, e-mail, téléphones, numéro de compte,
numéro d'entreprise, responsable, signature) ajoutée à `Activities/Edit`/`Details` — chaque champ,
laissé vide, doit reprendre celui de l'organisation. Dates, groupes et formulaire renvoient vers les
écrans déjà existants (wizard, `ActivityGroupsController`).

**Modèle de questions par activité** : `OrganisationQuestionTemplate`, page `/QuestionTemplates`,
copié automatiquement dans les questions de chaque nouvelle activité (wizard, création simple,
import CSV), puis librement modifiable par activité.

## Lot K — Nouvelles demandes

2 adresses e-mail par parent, envoi de mail filtré par semaine, quota d'inscription par année de
naissance, prévu/payé sur les excursions, garderie (accueil extrascolaire) comme service optionnel,
attestation mutuelle générée automatiquement (calquée sur `GERARD_MULLER_-_S2.pdf`), un seul badge
« aîné » par famille sur les listes, édition/suppression de lignes de comptes.

## Bug connu et corrigé

`Payments/Create` : sous la culture fr-BE (où `.` est un séparateur de milliers), un montant tapé
comme « 38.50 » était silencieusement interprété comme 3850 (×100), sans erreur. Corrigé par un
binder de modèle dédié (`DecimalInputHelper`/`DecimalModelBinder`), qui traite le dernier séparateur
tapé comme le séparateur décimal — corrige tous les champs `decimal` de l'app, pas seulement celui-ci.

## Infra — Migration Azure → VPS OVH

Hébergement migré vers un VPS OVH (Ubuntu 24.04, Docker, Caddy/Let's Encrypt), PostgreSQL à la
place de SQL Server, CI/CD via GHCR (voir [ADR 0012](../adr/0012-cicd-ovh-vps-via-ghcr.md)). Azure
entièrement décommissionné. Paiement en ligne : Stripe et Mollie tous les deux disponibles,
sélection par configuration (`Payments:Provider`, voir [ADR 0010](../adr/0010-online-payments-provider-agnostic-stripe.md)).
