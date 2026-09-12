# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier (certaines annotées par Thomas en rouge/vert) + exemple [`17.pdf`](17.pdf) (listing ONE).

**Mise à jour 2026-07-30** — 5 des 10 questions posées après la relecture complète ont été tranchées par le user : attestations fiscales (par association), présences équipe (confirmé), richesse de la liste des groupes (confirmé, avec une précision), impression groupée des groupes (confirmé), ventilation ONE des présences (confirmé). Les items confirmés et codables sans blocage ont été livrés (Lots C, D, E). Une nouvelle question est apparue en construisant Lot H (besoin d'un exemple réel d'attestation fiscale) : il reste 6 questions ouvertes pour Thomas.

**Mise à jour 2026-07-31 (Notion)** — Thomas a ajouté 2 nouvelles pages à l'export : **« Création d'une activité »** (refonte du formulaire de création en assistant multi-étapes, entièrement nouvelle — voir Lot I) et **« Paramètres »** (liste de champs texte, sans maquette — voir Lot J). La page « Tableau de bord de l'activité » a aussi été complétée (voir Lot A). Ça ajoute 1 nouvelle question ouverte (n°7, modèle de questions par activité) et 1 nouveau bug signalé (téléchargement du code d'intégration iframe qui produit un fichier 0 ko sur Mac). Aucune des 6 questions précédentes n'est résolue dans cet export.

**Mise à jour 2026-07-31 (infra)** — Migration complète de l'hébergement Azure → VPS OVH réalisée dans la foulée (voir section *Infra* ci-dessous) : les 2 items TO-DO « Passer à OVH » et « Créer un compte Brevo dédié » sont maintenant ✅ Fait ; la décision « Passer à Molly » a été **annulée** (on garde Stripe).

**Mise à jour 2026-07-31 (réponses)** — Olivier a tranché les questions 1 à 5 (détail dans les lots concernés) : paiements partiels/CPAS confirmés (Lot C), menu hamburger totalement supprimé (Lot A), numéro de ticket remis à 0 par activité (Lot D), définition de « Hors bilan » précisée (Lot D), périmètre de l'auto-proposition mail Excursion clarifié (Lot F). Il ne reste que les questions 6 (attestations fiscales) et 7 (modèle de questions par activité).

**Mise à jour 2026-07-31 (livraison)** — Lots A, C, D, E, F et G codés, testés et déployés en production. Détail dans les sections concernées.

**Mise à jour 2026-07-31 (relecture complète)** — Relecture détaillée de toutes les pages Notion + captures (43 images + `17.pdf`) pour vérifier que rien n'avait été manqué dans les synthèses précédentes. Résultat : aucune nouvelle demande non triée, mais 1 erreur de citation corrigée (Lot I, étape 2 — la bonne capture annotée en rouge est `10.25.06`, pas `09.19.42`) et 2 nuances ajoutées (Lot A : Thomas ne barre que 3 boutons « Actions rapides » sur 5, les 5 ont été retirés — à confirmer ; Lot C : ventilation « Total prévu EXCURSIONS » de la maquette absente de `Bookings/Details`, non bloquant).

**Mise à jour 2026-09-12 (Notion — nouvelle note + 2ᵉ relecture exhaustive)** — Thomas a ajouté 3 fichiers à l'export : un exemple d'**attestation mutuelle** (`GERARD_MULLER_-_S2.pdf`, commune de Clavier), une capture de l'app Notes listant **8 nouvelles demandes** (voir Lot K), et une image d'avatar Notion sans valeur fonctionnelle. Une 2ᵉ relecture exhaustive (texte + **toutes** les images/PDF, cette fois par des agents dédiés indépendants) de l'intégralité des pages a par ailleurs trouvé **2 écarts réels** manqués par la relecture du 2026-07-31, tous deux tranchés par Olivier le 2026-09-12 :
- **Lot A** — Thomas demandait explicitement une page d'accueil en gros boutons « Titre + Dates » par stage (comme l'écran de sélection d'activité), pas juste une liste allégée. **Décision : gros bouton par stage, comme demandé.**
- **Lot B** — la maquette montrait un montant **éditable manuellement** par le coordinateur à la confirmation (capture `19.49.08`) ; le flux livré l'avait entièrement automatisé sans possibilité d'ajustement. **Décision : réintroduire la possibilité d'ajustement manuel.**

Le reste des pages (Comptes, Emails, Équipe, Excursions, ONE, Présences, Tableau de bord, Hamburger, Paramètres, Inscriptions, reste de Création d'activité) a été confirmé conforme à ce backlog, annotation par annotation — aucun autre écart trouvé.

---

## ⚠️ Questions ouvertes pour Thomas

1. **Attestations fiscales** (Lot H) — besoin d'un exemple réel (mise en page, mentions légales obligatoires, montant déductible, période couverte…), comme `17.pdf` l'a été pour le rapport ONE. Sans ça, le risque est de construire un document non valable fiscalement.
2. **Modèle de questions par activité** (Lot I, nouveau 2026-07-31) — Thomas demande, dans la capture `09.49.21` de « Création d'une activité » : *« On fonctionne comme pour les mails avec un modèle qui englobe toutes les questions. Par défaut c'est tout ce que l'organisation a demandé mais c'est modifiable par activité ? »*. Même logique que les modèles d'email verrouillés (Lot E) à confirmer/adapter pour les questions personnalisées.

*(Résolues et retirées de cette liste — détail dans le lot concerné : explication paiement/dépense → Lot D ; envoi depuis un modèle et conflit Lot 4 → Lot E ; attestations fiscales, présences équipe, richesse des groupes, impression groupée, ventilation ONE → voir Lots C/G/H ci-dessous ; paiements partiels/CPAS (Olivier, 2026-07-31) → Lot C ; menu hamburger (Olivier, 2026-07-31) → Lot A ; numéro de ticket (Olivier, 2026-07-31) → Lot D ; Hors bilan (Olivier, 2026-07-31) → Lot D ; auto-proposition mail Excursion (Olivier, 2026-07-31) → Lot F.)*

## 📌 TO-DO (hors backlog UX)

- ✅ **Fait (2026-07-31)** — ~~Passer à OVH (VPS-2)~~ : migration complète Azure → VPS OVH (`vps-5f0be0bf.vps.ovh.net`, `new.cedeva.be`) réalisée en 5 phases avec migration PostgreSQL incluse. Détail complet dans la section *Infra* ci-dessous.
- ✅ **Décision annulée (2026-07-31)** — ~~Passer à Molly~~ : on **garde Stripe** comme solution de paiement en ligne. `Stripe.net` mis à jour 47.4.0 → 52.2.0 (corrige un rejet de webhook pour incompatibilité de version d'API), testé de bout en bout en mode test (checkout + webhook).
- ✅ **Fait (2026-07-31)** — ~~Créer un compte Brevo dédié pour Cedeva~~ : utilisation du compte Brevo existant de Thomas (Kivla srl), domaine `cedeva.be` authentifié (SPF/DKIM), clé API dédiée générée et configurée sur le VPS, IP du VPS ajoutée à l'allowlist Brevo. Testé de bout en bout (email de confirmation d'inscription reçu).
- ✅ **Fait (2026-07-31)** — ~~Passer TinyMCE en self-hosted (GPL)~~ : en creusant, TinyMCE n'était en réalité utilisé nulle part dans l'app (l'éditeur riche réel est Summernote, chargé depuis `cdn.jsdelivr.net`). Config morte supprimée : section `TinyMCE:ApiKey` (`appsettings.json`) et entrées CSP `cdn.tiny.cloud` (`SecurityHeadersMiddleware.cs`).
- ✅ **Résolu (2026-07-31)** — 🐛 Bug signalé (capture `09.54.00` annotée) : Thomas ne pouvait pas télécharger le fichier « Register » (0 ko sur Mac) sur l'écran final « Création d'une activité » (code d'intégration iframe / bouton de téléchargement). Thomas a retesté après la migration Azure → OVH : ça fonctionne maintenant. Cause exacte non diagnostiquée (le bug a disparu avec le changement d'infra, pas de root cause confirmée côté code).
- 🐛 **Bug découvert (2026-07-31, pas encore corrigé)** — Le champ Montant de `Payments/Create` rejette parfois la saisie en validation côté client (« Veuillez fournir une valeur multiple de 0.01 » / valeur jugée invalide) selon le format décimal tapé (point vs virgule). Trouvé en testant Lot C, sans rapport avec les changements du jour — probablement un souci de culture (`fr-BE`) dans la validation jQuery unobtrusive générée pour le champ `Amount`. À investiguer séparément.

---

## Infra — Migration Azure → VPS OVH (2026-07-31)

Migration complète menée en une session, en 5 phases séquentielles (chaque phase vérifiée avant de passer à la suivante) :

- **Phase A — SQL Server → PostgreSQL** : swap du provider EF Core (`Npgsql.EntityFrameworkCore.PostgreSQL`), migration baseline unique régénérée, `BelgianMunicipalityService` réécrit en comparaisons `ToLower()` portables (`EF.Functions.ILike` essayé d'abord, cassait la suite SQLite — Npgsql-only), `AzureBlobStorageService` supprimé au profit de `LocalFileStorageService` partout, switch Npgsql `EnableLegacyTimestampBehavior` (l'app ne trackait pas `DateTimeKind`, cassait le seeding sur les colonnes `timestamptz`).
- **Phase B — VPS durci** : Ubuntu 24.04 LTS, SSH par clé uniquement (mot de passe désactivé), pare-feu `ufw` (22/80/443), Docker Engine + Compose (dépôt officiel), `fail2ban`, swap 2 Go.
- **Phase C — Stack de prod** : `docker-compose.prod.yml` (app + PostgreSQL + Caddy), HTTPS automatique Let's Encrypt pour `new.cedeva.be`.
- **Phase D — CI/CD** : nouveau workflow [`deploy-vps.yml`](../../.github/workflows/deploy-vps.yml) (build/test inchangés → build & push image sur GHCR → déploiement SSH → gate `/health`), remplace `main_cedeva-demo.yml` (Azure). Voir [ADR 0012](../adr/0012-cicd-ovh-vps-via-ghcr.md), supersède [ADR 0007](../adr/0007-cicd-azure-app-service-with-health-gate.md).
- **Phase E — Décommissionnement Azure** : resource group `cedeva-rg` supprimé en entier (SQL Server, App Service, Storage, Application Insights…) + un espace de travail Log Analytics résiduel trouvé hors du resource group et supprimé aussi. Souscription Azure entièrement vide, confirmé via `az resource list`.

**Bug de production découvert et corrigé pendant les tests** : le trousseau de clés Data Protection (`/root/.aspnet/DataProtection-Keys`) n'était pas persisté entre redémarrages du conteneur — chaque déploiement invalidait silencieusement toutes les sessions/jetons anti-CSRF/TempData en cours (`CryptographicException: key not found in the key ring`), cassant le formulaire d'inscription public en plein milieu. Corrigé par `PersistKeysToFileSystem` pointé sur un volume Docker nommé (`cedeva-dpkeys`).

**Tests de bout en bout réalisés sur `new.cedeva.be`** : inscription publique (parent + enfant) via l'iframe, paiement Stripe (mode test, checkout + webhook), email de confirmation Brevo — les trois fonctionnent.

---

## Lot A — Accueil & Navigation

- ✅ **Fait** — Tableau de bord d'activité : clic sur le **titre** → 7 gros boutons (`ActivityManagement/Index`) ; clic sur **« Paramètres »** (renommé, ex-« Gérer ») → réglages de l'activité (`Activities/Details`).
- ✅ **Fait** — Page d'accueil réduite à la liste « Activités récentes » (4 cartes stats, inscriptions récentes, actions rapides retirées, `HomeController` simplifié en conséquence). ⚠️ **Nuance (relecture 2026-07-31)** : sur `18.08.04`, Thomas ne barre en rouge que 3 des 5 boutons « Actions rapides » (Nouvelle inscription, Nouveau parent, Nouveau membre équipe) — il laisse « Nouvelle activité » et « Nouvel enfant » non barrés. Les 5 ont été retirés. À confirmer avec Thomas si ces 2 raccourcis manquent, sinon rien à changer.
- ✅ **Fait (2026-09-12)** — La demande initiale (texte de la page « Page d'accueil générale organisation ») allait plus loin que la simple suppression de widgets : Thomas voulait arriver sur une page en **gros boutons par stage** (un bouton = Titre + Dates de l'activité, comme l'écran de sélection d'activité déjà existant dans l'app). `Home/Index.cshtml` : la liste `list-group` (titre + dates + nombre de réservations + bouton « Gérer » séparé) remplacée par une grille de gros boutons cliquables (titre + dates uniquement).
- ✅ **Fait** — Menu du haut (dans une activité) réduit à « Tableau de bord » + dropdown « Pages spéciales » (liste des groupes, total des présences).
- ✅ **Fait (2026-07-31)** — Menu hamburger **supprimé totalement** : tous ses liens (Contacts, Importer des parents/enfants, Équipe compris) redescendent en dessous sur la page d'accueil, dans une nouvelle section « Paramètres généraux ». Plus de barre latérale globale du tout.
- ✅ **Fait (2026-07-31)** — Le tableau de bord d'activité est revenu à **8 grosses tuiles** (comme la maquette d'origine `18.09.45`) : le bouton séparé « Paramètres de l'activité » est maintenant la 8ᵉ tuile de la grille, ne reste en dessous que le bouton « Sortir de cette activité ».

## Lot B — Confirmation des inscriptions

- ✅ Déjà le cas : ne montre que les inscriptions « à confirmer ».
- ✅ **Fait** — Clic sur le nom de l'enfant → fiche `Bookings/Details` (voir Lot C).
- ✅ **Fait (2026-07-31, révision du flux)** — Le flux a été redéfini avec Olivier : le parent ne paie plus « en direct » à l'inscription ; à la confirmation par le coordinateur (`ManageBookings`), un mail avec lien de paiement Stripe (carte + Bancontact) et QR code est envoyé automatiquement au parent si un solde reste dû. Groupe et fiche médicale ont été **découplés** de l'acte de confirmation (retirés de `ManageBookings`, restent assignables via `Bookings/Edit` et l'écran `GroupAssignment`). Écran mort `UnconfirmedBookings` (non lié depuis l'UI) supprimé au passage. Pas d'expiration sur le lien (décision Olivier). ⚠️ Bancontact doit être activé côté Dashboard Stripe pour le compte live.
- ✅ **Fait (2026-09-12)** — La maquette d'origine (`Inscriptions/19.49.08`) montrait un champ **« Total prévu à payer » éditable manuellement** par le coordinateur juste avant de valider l'inscription (encadré rouge). Réintroduit sur `ManageBookings.cshtml` (champ montant pré-rempli avec `Booking.TotalAmount`, ajustable avant de cliquer sur « Confirmer » — `ConfirmBookingRequest.AdjustedTotalAmount`, recalcule `PaymentStatus`).

## Lot C — Présences

- ✅ **Fait** — Clic sur un enfant → fiche `Bookings/Details` enrichie (adresse/parent éditables, groupe, fiche médicale, historique des paiements).
- ✅ **Fait (2026-07-31)** — Ligne « Dont excursions » sur `Bookings/Details.cshtml`, sous le montant total (affichée seulement si > 0) : somme des `Excursion.Cost` des excursions auxquelles l'enfant est inscrit (déjà incluse dans `Booking.TotalAmount`, ajoutée par `ExcursionService.RegisterChildAsync`) — reprend l'esprit de la ligne « Total prévu EXCURSIONS » de la maquette `Présences/18.29.32`.
- ✅ Confirmé dans le code : le filtre jour est déjà scopé à l'activité + jour sélectionné.
- ✅ **Fait** — Colonne « Payé » (✓/✗ + solde) dans `Presences.cshtml`.
- ✅ **Fait (2026-07-31)** — Forcer une inscription non payée : badge de statut de paiement (+ solde) sur `ManageBookings` ; avertissement de confirmation (`confirm()` JS avec le solde dû). Rien ne bloquait techniquement la confirmation, le manque était la visibilité — désormais remplacé par l'envoi automatique du mail de paiement (voir Lot B). Solde restant recalculé en direct pendant la saisie sur `Payments/Create` ; testé avec 2 paiements manuels successifs sur la même réservation.
- ✅ **Fait** — Pages spéciales, enrichies au niveau demandé par Thomas :
  - **Liste des groupes imprimable** (`Groups`/`PrintGroups`) : sélection **multiple** de groupes (au lieu d'un seul à la fois, `<select multiple>`), options à cocher **Prévus/Présent/Signature** pour choisir les colonnes affichées, colonne **Signature** vide (émargement papier), **export PDF** et **export Excel** (`ExportGroupsPdf`/`ExportGroupsExcel`, réutilisent `IExportFacadeService` déjà utilisé ailleurs) en plus de l'impression navigateur existante. Rappel : les libellés « 3-4, 5-6, 7-8… » des captures sont juste les groupes de l'activité (comme nos « Groupe Rouge/Bleu/Vert »), pas une tranche d'âge — rien changé côté structure des groupes.
  - **Fait — « Imprimer tous les groupes du jour »** en un clic : bouton dédié sur `Groups.cshtml` (visible seulement si l'activité a un jour programmé aujourd'hui), distinct de l'impression filtrée groupe par groupe.
  - ✅ **Fait** — Total des présences journalières (`PresenceSummary`) : décomposé par indicateur ONE (Milieu défavorisé / Handicap léger / Handicap lourd) en plus du total brut réservé/présent (réf. capture `18.48.14`).

## Lot D — Comptes / Finances

- ⏸️ **Pas fait** — Simplification du parcours Comptes → Transactions : la cible (captures `18.48.58 1`/`18.50.34`) est de sauter directement sur la liste des transactions nue (sans les cartes stats ni les onglets de filtre) en cliquant sur « Comptes ».
- ✅ **Fait** — Bouton « Masquer les montants » sur `Transactions.cshtml`.
- ✅ **Fait (2026-07-31)** — Numéro de ticket unique par ligne : `Payment.TicketNumber`/`Expense.TicketNumber`, séquence **partagée** entre les deux tables, **remise à 1 à chaque nouvelle activité** (via `GetNextTicketNumberAsync`, `MAX(...) WHERE ActivityId = X` + 1). Colonne « N° ticket » sur `Transactions.cshtml`, affiché aussi sur `Payments/Details.cshtml`. Migration avec backfill des tickets existants (interleave chronologique Payment+Expense par activité). Champ **« Caisse / Compte »** : déjà couvert par le modèle existant (`Payment.PaymentMethod` Cash/BankTransfer, `Expense.OrganizationPaymentSource` OrganizationCard/OrganizationCash) — pas de nouveau champ ajouté, contrairement à ce que ce backlog supposait.
- ✅ **Fait** — Fusion Ajouter un paiement / Ajouter une dépense (`Financial/AddTransaction`, onglets Paiement/Dépense) : `Payment` (clé = réservation) et `Expense` (clé = activité) n'ayant ni la même clé ni les mêmes champs, la fusion est un écran unique à onglets hébergeant les deux formulaires existants inchangés (POST vers `PaymentsController.Create` / `FinancialController.CreateExpense`). Les 2 boutons de `Transactions.cshtml` pointent maintenant vers cet écran unique.
- 🐛 **Bug corrigé** — Clé de session incohérente entre `PaymentsController` (`"FinancialActivityId"`) et `FinancialController` (`"Financial_ActivityId"`) : le filtre par activité de `SelectBooking` était un no-op en production (listait toutes les réservations impayées de l'org, pas seulement celles de l'activité courante).
- ✅ **Fait (2026-07-31)** — Catégories : `ExpenseCategory.IsIncome` (bool) remplacé par `CategoryType` (enum **Expense/Income/OffBalance**, 3 valeurs comme sur `18.58.21`/`19.00.10`) + `Budget`. Nouvelle FK structurelle `Expense.ExpenseCategoryId` (les dépenses n'étaient rapprochées d'une catégorie que par nom en texte libre) avec migration de rapprochement automatique par nom. **« Hors bilan »** : les dépenses dont la catégorie est `OffBalance` sont exclues des totaux Entrées/Sorties partout (`Transactions`, `Index`, `Report`), affichées dans leur propre section (carte + badge dédiés). Champ **« Lié à un enfant ? »** de la référence toujours absent — à revoir séparément si besoin.
- ✅ Déjà 100% auto-calculé, vérifié dans le code : catégories Équipe (Sorties) et PAF (Entrées).
- ✅ **Fait (2026-07-31)** — Rapport détaillé par catégorie (tableau groupé Nom/Nombre/Montant, dépenses d'organisation uniquement pour rester cohérent avec le total du résumé final) + section Hors bilan séparée sur `Report.cshtml`. Pas encore de colonne Budget par catégorie dans ce tableau (`ExpenseCategory.Budget` existe mais n'est pas encore affiché en regard du réalisé) — amélioration possible ultérieure.

## Lot E — E-mails

- ✅ **Fait (2026-07-30, commit `9fe7f51`)** — Épurer l'UI de `SendEmail.cshtml` : panneau Informations retiré, boutons « Enregistrer comme modèle »/« Historique » sortis de la rangée du bas (déplacés en bandeau compact en haut d'écran), texte d'aide de « Un email par enfant » replié dans le label, panneau Variables de personnalisation replié par défaut (`<div class="collapse">`). Corrigé au passage 2026-07-31 : le backlog disait encore « Pas fait » alors que le travail était déjà livré la veille.
- ✅ **Fait** — 3 modèles verrouillés (Confirmation d'inscription, Rappel fiche médicale, Rappel paiement) : uniques par organisation, non créables/dupliquables/supprimables, plus jamais copiés par activité (migration de nettoyage des copies déjà existantes).
- ✅ Déjà le cas — modèles Excursion libres.
- ✅ **Fait** — Bouton « Envoyer » sur `EmailTemplates/Index` → ouvre `SendEmail` avec le modèle pré-chargé.

## Lot F — Excursions

- ✅ **Fait** — Formulaire Créer/Modifier : Heure début/fin et Type retirés de l'écran (champs cachés, valeurs préservées — pas supprimés du modèle). Nom/Description/Date/Coût/Groupes restent.
- ✅ **Fait** — Liste « Gérer les excursions » : colonnes Type et finances retirées.
- 🐛 **Bug corrigé** — `Excursions.SendEmail` n'envoyait jamais rien réellement ; corrigé.
- ✅ **Fait (2026-07-31)** — Nouveau type de destinataire sur `Excursions/SendEmail` : « Inscrits à l'activité, pas encore à cette excursion » (`not_yet_registered`), anti-join `Bookings`/`ExcursionRegistrations` (bookings confirmés de l'activité parente sans registration sur l'excursion visée). ⏸️ **Reste à faire** : l'« auto-proposition » proprement dite (suggérer/pré-remplir l'envoi automatiquement à la création ou programmation d'une excursion) n'est pas implémentée — seul le nouveau destinataire manuel dans `SendEmail` l'est.

## Lot G — Équipe

- ✅ **Fait** — Panneau « Membres disponibles » déplacé sous « Équipe assignée », replié par défaut.
- ✅ **Fait** — Présences équipe jour/jour (miroir du système enfants) : nouvelle entité `TeamMemberDay` (+ migration avec backfill des assignations existantes en présent, pour ne rien changer rétroactivement aux salaires déjà calculés), page `TeamPresences` (sélecteur de jour + case à cocher par membre, même mécanisme AJAX que `Presences`), dans le dropdown « Pages spéciales ». Les lignes de présence sont créées/supprimées automatiquement à l'assignation/retrait d'un membre et à l'activation/désactivation d'un jour (formulaire d'édition, éditeur AJAX +/- jour, changement de plage de dates). Le calcul salarial (`FinancialCalculationService`, `FinancialController` Index/TeamSalaries/ExportTeamSalaries/Report) utilise désormais le nombre réel de jours cochés « présent » par membre au lieu de supposer 100% des jours de l'activité.
- ✅ Déjà satisfait, vérifié : compléments/dépenses par membre (`Expense.TeamMemberId`), décompte total par personne (`TeamSalaries.cshtml`).
- ✅ **Fait** — Stockage de l'extrait de casier judiciaire (`TeamMember.CriminalRecordUrl`).

## Lot H — ONE (organisme officiel)

- ✅ **Fait** — 4 tableaux par activité (`ActivityManagement/OneReport`) : listings 2-5 ans / 6 ans et plus (N°, nom, âge, dates, jours, prix payé, indicateurs) + présences hebdomadaires par tranche d'âge. Format calqué sur [`17.pdf`](17.pdf). Aucune migration nécessaire, testé (`OneReportTests.cs`). **Ce rapport reste par activité** (c'est un rapport officiel envoyé à l'ONE, distinct de l'attestation fiscale ci-dessous — à ne pas confondre).
- ⏸️ **Bloqué — en attente d'un exemple de Thomas (question n°1)** — Attestations fiscales : **regroupées par association**, pas par activité (confirmation du texte original — l'attestation fiscale donnée au parent est un document différent du rapport ONE par activité ci-dessus). Aucune attestation fiscale n'existe encore dans le code. Contrairement au rapport ONE (qui avait `17.pdf` comme référence exacte), on n'a **aucun exemple de mise en page/contenu réel** pour ce document officiel (mentions légales, montant déductible, période, etc.) — **à demander à Thomas avant de coder**, pour éviter de produire un document qui ne serait pas valable fiscalement.

## Lot I — Création d'une activité (refonte wizard) — ✅ Fait le 2026-08-01

Demande de refonte du formulaire de création d'activité : actuellement un formulaire plat unique (Titre/Du/Au), Thomas veut un **assistant multi-étapes avec jauge de progression** (maquette générique `10.21.29`, pastilles 1-2-3-4). Livré : nouveau `ActivityWizardController` (`Features/ActivityWizard/`), jauge `_WizardProgress.cshtml`, 9 nouveaux champs nullable sur `Activity` (migration `AddActivityWizardFields`). Détail des 7 étapes :

- ✅ **Fait** — **Écran d'entrée** : bouton vert « Créer une nouvelle activité » bien visible sur `Activities/Index`, pointe vers l'étape 1 du wizard (formulaire plat `Create` conservé mais plus mis en avant). Déconnexion déjà présente globalement dans le layout.
- ✅ **Fait** — **Étape 1** : Titre + Dates — crée l'`Activity` au clic sur Enregistrer, puis redirige vers l'étape 2.
- ✅ **Fait** — **Étape 2 — Paramétrage des dates** : boutons « Ajouter un jour avant/après », « Retirer le 1er/dernier jour » et bandeau info supprimés. Remplacés par un bouton « Ajouter une date » (calendrier) + un toggle « Regrouper par semaine » (JS pur, non persisté, coche/décoche les jours ouvrés d'une semaine via une case maîtresse).
- ✅ **Fait** — **Étape 3 — Règlement (R.O.I.)** : capture `09.31.41` relue plus précisément en cours d'implémentation — c'est un **champ URL** (lien vers le PDF hébergé ailleurs) + texte de case à cocher, **pas un upload de fichier**. `RegulationLinkUrl`/`RegulationAcceptanceText`, branchés jusque sur le formulaire public (case à cocher requise avant inscription si renseigné).
- ✅ **Fait** — **Étape 4 — Limitations** : codes postaux autorisés/refusés (existant) + nouveaux `PostalCodeErrorMessage`, `MaxChildrenPerDay`, `FullMessage`. Le plafond est appliqué côté formulaire public comme un cap sur le nombre total d'inscriptions actives de l'activité (pas un vrai comptage par jour calendaire — approximation pragmatique, à affiner si Thomas la juge insuffisante).
- ✅ **Fait** — **Étape 5 — Autres questions** : éditeur de questions réutilisé, toggle « Actif » retiré du formulaire (round-trippé via un champ caché — les nouvelles questions restent actives par défaut). La question n°2 (modèle de questions org→activité) reste hors scope, comme convenu.
- ✅ **Fait** — **Étape 6 — Affichage** : `PublicationStartDate`/`PublicationEndDate`/`NoActiveFormMessage`/`RedirectUrlAfterSubmit`, branchés sur le formulaire public (fenêtre de publication, redirection personnalisée après envoi).
- ✅ **Fait** — **Étape 7 — Final** : redirige vers l'écran de personnalisation iframe existant (`PublicRegistration/EmbedCode`, pas `Activities/Details` comme supposé initialement), inchangé.

Les 4 groupes de champs neufs (Règlement, Limitations, Affichage, redirection) sont branchés à la fois sur le flux d'inscription simple (`Register`, celui réellement utilisé par le code d'intégration iframe généré) et sur le flux multi-étapes (`SelectActivity`/`ActivityQuestions`/`CreateBooking`). Vérifié par 8 nouveaux tests d'intégration + parcours navigateur complet des 7 étapes (Playwright). 🐛 Bug trouvé et corrigé en cours de route : les vues du wizard avaient été placées sous `Features/Activities/` au lieu de `Features/ActivityWizard/` (la convention feature-folder de l'app associe le dossier de vues au nom du contrôleur), ce qui cassait chaque étape avec une 500.

⚠️ **Lacune découverte le 2026-09-12** : les 8 tests mentionnés ci-dessus couvrent le branchement des champs côté `PublicRegistrationController`, mais **`ActivityWizardController` lui-même (Step1 à Step7, `AddDate`) n'a aucun test automatisé** — vérifié uniquement par le parcours Playwright manuel au moment de la livraison, jamais figé en test de régression. Seule l'étape 4 (gestion des quotas par année de naissance, ajoutée aujourd'hui) a des tests. À combler séparément si ce contrôleur est retouché.

## Lot J — Paramètres (nouveau 2026-07-31, sans maquette)

Page listée dans l'export mais sans capture d'écran associée — juste une liste de champs à formaliser :

- ⏸️ **À maquetter** — **Signalétique de l'activité** : Logo, Titre, Adresse, E-mail (reply-to des mails envoyés), Téléphone 1, Téléphone 2, Numéro de compte, Numéro d'entreprise, Nom du responsable, Signature du responsable. Reprise par défaut des paramètres généraux de l'organisation, adaptable par activité.
- ⏸️ **À maquetter** — Dates de l'activité (probablement un renvoi vers l'Étape 2 du wizard de création, Lot I).
- ⏸️ **À maquetter** — Groupes de l'activité.
- ⏸️ **À maquetter** — Formulaire (probablement un renvoi vers les Étapes 5/6 du wizard, Lot I).

## Lot K — Nouvelles demandes (note du 2026-08-24 + écarts retrouvés le 2026-09-12)

Toutes les décisions ci-dessous ont été tranchées par Olivier le 2026-09-12.

- ⏸️ **Pas fait** — **Page d'accueil en gros boutons par stage** (écart Lot A, voir ci-dessus) : remplacer la liste `list-group` de `Home/Index.cshtml` par une grille de gros boutons, un par activité, affichant uniquement Titre + Dates (réutilise l'esprit de l'écran de sélection d'activité public), clic → `ActivityManagement/Index`.
- ⏸️ **Pas fait** — **Ajustement manuel du montant à la confirmation** (écart Lot B, voir ci-dessus) : réintroduire sur `ManageBookings` un champ montant éditable (pré-rempli avec `Booking.TotalAmount`) avant validation, sans casser l'envoi automatique du mail de paiement sur le solde qui en résulte.
- ✅ **Fait (2026-09-12)** — **2 adresses e-mail par parent** : `Parent.SecondaryEmail` (migration `AddParentSecondaryEmailAndExcursionPaidAmount`), `Parent.GetEmailAddresses()` (helper partagé, dédupliqué) inclus dans tous les envois (`EmailRecipientService`, confirmation de réservation, mails d'excursion) — hors formulaire d'inscription publique (admin uniquement, scope volontairement limité). Champ sur `Parents/Create.cshtml`/`Edit.cshtml`.
- ✅ **Fait (2026-09-12)** — **Envoi de mail filtré par semaine** : nouveau paramètre `weekNumber` sur `IEmailRecipientService.GetRecipientEmailsAsync` (filtre `ActivityDay.Week`, cumulable avec le filtre par jour), sélecteur « Prévu la semaine » sur `SendEmail.cshtml` (`ActivityManagement`).
- ✅ **Fait (2026-09-12)** — **Quota d'inscription par année de naissance** — décision (Olivier) : les enfants s'inscrivent en général à la semaine ; **en pratique une réservation réserve toujours tous les jours actifs de l'activité en une fois** (pas d'inscription partielle par semaine dans le flux actuel), donc un quota par (activité, année de naissance) se comporte déjà comme un quota « par semaine » dans le cas courant (1 activité = 1 semaine de stage) — approximation pragmatique documentée dans le code, à affiner si un jour le flux permet une inscription partielle. Nouvelle table `ActivityBirthYearQuota` (`ActivityId`, `BirthYear`, `MaxChildren`, index unique sur le couple), `Activity.BirthYearQuotaExceededMessage` (message personnalisable), vérifié dans `PublicRegistrationController` (`Register` POST, `CreateBooking` du flux multi-étapes) via `CheckBirthYearQuotaAsync`. Gestion des quotas ajoutée à l'étape 4 (Limitations) du wizard (`ActivityWizardController.AddBirthYearQuota`/`RemoveBirthYearQuota`).
- ✅ **Fait (2026-09-12)** — **Prévu/payé sur les excursions** : `ExcursionRegistration.PaidAmount` (le "prévu" reste `Excursion.Cost`, commun), champ éditable par ligne dans `Excursions/Registrations.cshtml` (AJAX, `ExcursionsController.UpdatePaidAmount`).
- ✅ **Fait (2026-09-12)** — **Garderie (accueil extrascolaire), comme service optionnel** — nouvelle entité `ChildcareRegistration` (`BookingId`, `ActivityDayId`, `Amount`, index unique sur le couple), `Activity.ChildcarePricePerDay` (tarif par défaut, configurable sur `Activities/Edit`, éditable par ligne). Nouvelle page `ActivityManagement/Childcare` (sélecteur de jour + case à cocher + montant par enfant, AJAX, ajoutée au menu « Pages spéciales ») — c'est à la fois l'écran de gestion **et** la liste des enfants inscrits à la garderie ce jour-là. Montant inclus dans `Booking.TotalAmount` comme pour les excursions.
- ✅ **Fait (2026-09-12)** — **Attestation mutuelle, générée automatiquement, généralisée par organisation** — le PDF fourni (`GERARD_MULLER_-_S2.pdf`, commune de Clavier) a servi de modèle, généralisé : `Organisation.ResponsibleName` (nouveau champ, configurable sur `Organisations/Edit`) complète le logo et l'adresse déjà existants (`Organisation.LogoUrl`/`Address`). Contrairement au plan initial, **pas de nouveau service QuestPDF** : suivant le pattern déjà établi par `OneReport`/`Print`/`PrintGroups` (page HTML imprimable par le navigateur, pas de PDF généré serveur), nouvelle action `Bookings/MutualityAttestation/{id}` + vue dédiée, bouton sur `Bookings/Details`. Utilise les données déjà disponibles (`BookingDay.IsPresent` pour les jours réels de présence, `Booking.PaidAmount`).
- ✅ **Fait (2026-09-12)** — **1 aîné par famille sur les listes** — décision (Olivier) appliquée : ignore les familles recomposées, basé uniquement sur `Child.ParentId`. `PresenceChildInfo.IsEldestInFamily` (calculé dynamiquement, `ActivityManagementController.ComputeEldestChildIds`, pas de nouveau champ persistant), badge ⭐ affiché sur `Presences.cshtml`, `Groups.cshtml`, `Print.cshtml`, `PrintGroups.cshtml`.
- ✅ **Fait (2026-09-12)** — **Édition/suppression de lignes de comptes** : `PaymentsController.Edit`/`Delete` ajoutées (le pattern existait déjà côté `Expense`), avec réajustement de `Booking.PaidAmount`/`PaymentStatus` par delta (helper partagé `RecalculateBookingPaymentStatus`, aussi utilisé par `Cancel`).

---

## Ordre proposé

L'essentiel des Lots A à I est livré. Toutes les questions 1 à 5 d'origine étant tranchées par
Olivier, **plus aucun item n'est bloqué en attente d'une réponse** sauf Lot H (attestations
fiscales) et Lot J (maquette manquante). Reste à coder, par priorité :

1. **Petits restes** — tous livrés le 2026-07-31 sauf Lot F (mis de côté) :
   - ✅ Lot D — simplifier le parcours Comptes → Transactions (cartes stats + onglets de filtre retirés de `Transactions.cshtml`, liste nue).
   - ✅ Lot E — épuration de `SendEmail.cshtml` (en fait déjà livré le 2026-07-30, backlog seulement mis à jour aujourd'hui).
   - ✅ Lot C — ligne « Dont excursions » sur `Bookings/Details`.
   - ⏸️ Lot F — la vraie « auto-proposition » du mail Excursion (suggestion automatique à la création/programmation) ; seul le nouveau destinataire manuel existe. **Explicitement mis de côté pour l'instant.**
2. **Lot H** — attestations fiscales par association : bloqué en attente d'un exemple de Thomas (question n°1).
3. ✅ **Lot I** — wizard de création d'activité en 7 étapes, livré le 2026-08-01 (voir détail ci-dessus).
4. **Lot J** — Paramètres : à maquetter avec Thomas avant de coder (aucune capture fournie).
5. ✅ **Lot K** — nouvelles demandes du 2026-08-24 + 2 écarts retrouvés le 2026-09-12, toutes tranchées
   par Olivier et livrées le 2026-09-12 (10 items au total, détail dans la section Lot K ci-dessus) :
   2 e-mails parent, édition/suppression de paiements, prévu/payé excursions, filtre e-mail par
   semaine, aîné par famille, quota par année de naissance, page d'accueil en gros boutons,
   ajustement manuel du montant à la confirmation, garderie, attestation mutuelle généralisée.
   Suite de tests complète : 1299/1299 au vert (1268 avant ce lot). ⚠️ Lacune de test découverte au
   passage sur `ActivityWizardController` (Lot I) — voir note dans la section Lot I.

Bloqué par Thomas : Lot H (question n°1) et Lot J (maquette manquante). À confirmer avec Thomas
(non bloquant, cosmétique) : la nuance sur les 2 raccourcis « Actions rapides » (Lot A).
