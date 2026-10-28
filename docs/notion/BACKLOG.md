# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel

Source : export Notion du client (captures d'écran annotées) + exemples fournis (`17.pdf` pour le
rapport ONE, `GERARD_MULLER_-_S2.pdf` pour l'attestation mutuelle).

**Statut : lots A à K livrés. Lot L (retour du 26 octobre 2026) en cours — voir ci-dessous.**

## Points d'attention restants

- **Lot H — Attestation fiscale** : aucun exemple réel n'a jamais été fourni. Le document généré
  (`ParentsController.FiscalAttestation`) utilise des mentions légales génériques belges (frais de
  garde d'enfants, art. 113 CIR92) — **à faire relire par Thomas ou un comptable avant tout usage
  réel**. Un avertissement visible (non imprimé) le rappelle sur l'écran.
- **Lot I, étape 4 — `MaxChildrenPerDay`** : appliqué comme un plafond sur le nombre total
  d'inscriptions actives de l'activité, pas un vrai comptage par jour calendaire. Approximation
  pragmatique, à affiner si Thomas la juge insuffisante.
- ✅ **Fait (2026-09-12)** — **Sélection de semaine sur l'inscription publique.** Une activité
  couvre en général plusieurs semaines (créée comme une seule activité), et un enfant s'inscrit en
  général pour une seule semaine, pas toutes. L'inscription publique réservait auparavant
  systématiquement tous les jours actifs de l'activité entière, sans possibilité de choisir.
  Ajouté : une liste à cases à cocher (une par semaine, avec ses dates) sur les deux parcours
  publics (`Register`, l'iframe simple, et `ActivityQuestions` du parcours en plusieurs étapes) —
  n'apparaît que si l'activité a plus d'une semaine (`ActivityWeekOption`, regroupement par
  `ActivityDay.Week`). `Booking.TotalAmount` et les jours réservés (`BookingDay`) ne portent
  désormais que sur la ou les semaines cochées, avec un montant estimé recalculé en direct côté
  client. Le quota par (activité, année de naissance) (Lot K #4) est désormais lui aussi vérifié
  **par semaine** (`CheckBirthYearQuotaAsync`, comparé pour chaque semaine sélectionnée aux
  réservations existantes qui couvrent cette même semaine) — un enfant peut donc s'inscrire à une
  semaine dont le quota d'une autre semaine est atteint.
- **Lot J — signalétique de l'activité** : les champs (logo, adresse, téléphone, etc.) sont
  persistés et peuvent surcharger ceux de l'organisation, mais rien ne les consomme encore dans un
  document ou e-mail réel — le fallback reste à câbler au moment de l'utiliser.

## Lot L — Retour du 26 octobre 2026

Source : section « Améliorations 26 octobre 2026 » de la page Notion (9 sous-pages + « TODO Résumé »,
captures dans les dossiers `* 4054-*`). ⚠️ Thomas a relu une version **antérieure** aux commits du
15–19 septembre (non déployés à ce moment-là) : plusieurs remarques étaient déjà traitées dans le
code (présences équipe jour par jour, fiche membre modifiable, Décompte, menus par section,
signalétique affichant les valeurs de l'organisation, panneaux latéraux de la fiche enfant).

Les captures précisent le texte sur plusieurs points (repris ci-dessous) : la page BILAN a **deux**
sections (Entrées / Dépenses, avec colonnes Entrées / Sorties / Bilan / Budget en grisé) et une même
catégorie peut avoir des entrées et des sorties — le sens est porté par la transaction, pas par la
catégorie.

**Retouches rapides faites le 2026-10-28** (détail dans les messages de commit) :
- Accueil en grille de blocs, sans titre ni date ; page d'un stage sans compteurs (seul le badge
  « en attente » reste).
- Menus : gestion des inscriptions sans menu secondaire ; « Présences équipe » retiré du menu
  enfants ; « Voir les inscriptions » retiré des Paramètres ; Salaires et Décompte dans Équipe.
- Comptes : tableau de bord financier et boutons « Retour » supprimés ; plus d'onglet paiement dans
  « Ajouter une transaction » ; catégorie choisie dans une liste fermée.
- Emails : barre de boutons et « Un email par enfant » retirés de la rédaction ; filtre « Tous les
  types » retiré ; l'édition d'un modèle ne peut plus changer un type verrouillé (correctif).
- Paramètres : « Code HTML du formulaire » ; lien direct retiré ; dates, prix/jour et montant estimé
  retirés du formulaire public.
- Présences : bouton Imprimer retiré ; recherche dynamique.
- Fiche d'inscription : en attente → seulement jours, montant, gros bouton Confirmer, parent,
  questions ; confirmée → jours réservés en haut (sans dates de semaine), fiche médicale dans
  Informations, section Documents (attestation mutuelle + attestation fiscale du parent), bloc
  Statut et date d'inscription retirés.
- Paiement : « Ajouter un paiement à {enfant} », boutons radio Compte bancaire / Liquide (libellés
  renommés partout), panneau de droite retiré.
- Groupes : impression réduite au nom du groupe et à la date, sans Parent/Téléphone ; lignes plus
  hautes avec signature (HTML et PDF) ; date ajoutée au PDF et à l'Excel.
- Excursion : la page liste les enfants prévus ce jour-là dès la création (plus seulement après
  l'envoi du mail).

**Laissé de côté volontairement (à confirmer avec Thomas) :**
- « Voir tout » sur l'accueil : c'est le seul accès à Enfants, Parents, Modèles, Import (question 3).
- Sélecteur de langue et e-mail dans la barre du haut (barrés sur la capture) : barre commune à
  toute l'application.
- « Rapport » (Comptes) gardé tant que la page BILAN n'existe pas.
- « Attribué à » gardé tant que le lien au membre de l'équipe n'est pas déplacé (voir ci-dessous).
- « Modifier » sur une excursion (question 5).

**Gros chantiers faits le 2026-10-28** (choix validés par le user, à faire confirmer par Thomas) :
- **Comptes** : `Expense.IsIncome` (une ligne peut être une Recette) ; « Ajouter une transaction »
  façon démo (case verte Recette / rouge Dépense, n° de ticket, « Payé par » Caisse / Compte,
  catégorie fermée, explications). Remboursements/consommations de l'équipe : depuis la fiche membre.
- **Catégories** en 3 colonnes Entrées / Sorties / Hors bilan avec budget.
- **BILAN** (`Financial/Balance`, remplace le Rapport) : sections Entrées / Dépenses, colonnes
  Entrées / Sorties / Bilan / Budget ; P.A.F. = tous les paiements d'inscription (catégorie fixe) ;
  ligne « Défraiements de l'équipe » ; lignes Hors bilan exclues.
- **Dépannage** : un enfant inscrit mais pas prévu ce jour-là est ajouté réservé + présent, et le
  prix d'un jour est ajouté à son total.
- **Imprimer la semaine** des groupes : une feuille, une colonne par jour, une case les jours réservés.
- **Inscription au jour le jour** : réglage `Activity.AllowDayByDayRegistration` (étape Dates de
  l'assistant) ; le formulaire public propose alors des jours au lieu de semaines.
- **Emails d'inscription** : le modèle verrouillé envoyé à la soumission devient « Demande
  d'inscription reçue » (sans montant) — migration qui ne réécrit que les modèles non personnalisés ;
  la confirmation n'envoie que le lien de paiement (rien si rien à payer, y compris depuis
  `Bookings/Edit`).
- **Correctif** : date de naissance du formulaire public vide au lieu de 01/01/0001.
- **Justificatif joint** à une transaction (PDF/image), et rattachement des catégories saisies à
  la main (migration de données ; nouvelles catégories créées si besoin).
- **Finitions** : retour sur la fiche membre après ajout d'un complément, horaire/type d'excursion
  masqués, Décompte au format « DUPONT Pierre », un seul bouton Supprimer sur une inscription en
  attente, « -0,00 € » supprimés.

**Reste à faire :**
- **Emails — retirer « Type de modèle »** : impacte l'import/copie de modèles (un modèle par type)
  et le modèle par défaut des excursions — à repenser plutôt qu'à masquer.
- **Comptes — « Attribué à »** encore présent sur `CreateExpense`/`EditExpense` (fiche membre).
- **ONE — attestation fiscale** : attendre le modèle de PoM.

**Questions ouvertes pour Thomas :**
1. Budget des catégories par organisation (actuel) ou par activité ?
2. Si « Voir tout » disparaît de l'accueil, où placer Enfants, Parents, Modèles, Import ?
3. Excursion : un seul groupe ou plusieurs ? Montant facturé à la création de l'excursion ou à
   l'envoi du mail (actuel) ? « Modifier » est barré sur la capture : on retire l'édition ?
4. Un bénévole avec un tarif journalier renseigné doit-il être payé 0 ? (aujourd'hui le calcul
   ignore le statut)
5. Compléments équipe : garder 2 types (remboursement / consommation) ou des catégories
   (Formation, Prime, Sandwich…) ?
6. À confirmer : Caisse / Compte, P.A.F. fixe, dépannage facturé, inscription par jour en réglage
   d'activité (choix faits sans lui).

## Lot A — Accueil & Navigation

Page d'accueil réduite à une grille de gros boutons par activité (titre + dates), plus une section
« Paramètres généraux » regroupant les liens auparavant dans le menu hamburger (supprimé). Le
tableau de bord d'une activité affiche 8 tuiles (dont « Paramètres »), le menu du haut se limite à
un dropdown « Pages spéciales ».

## Lot B — Confirmation des inscriptions

Le parent ne paie plus à l'inscription : à la confirmation par le coordinateur (`ManageBookings`),
un mail avec lien de paiement en ligne (Mollie par défaut, carte + Bancontact) et QR code est envoyé automatiquement si
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
