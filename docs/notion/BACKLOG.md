# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier (certaines annotées par Thomas en rouge/vert) + exemple [`17.pdf`](17.pdf) (listing ONE).

**Mise à jour 2026-07-30** — 5 des 10 questions posées après la relecture complète ont été tranchées par le user : attestations fiscales (par association), présences équipe (confirmé), richesse de la liste des groupes (confirmé, avec une précision), impression groupée des groupes (confirmé), ventilation ONE des présences (confirmé). Les items confirmés et codables sans blocage ont été livrés (Lots C, D, E). Une nouvelle question est apparue en construisant Lot H (besoin d'un exemple réel d'attestation fiscale) : il reste 6 questions ouvertes pour Thomas.

**Mise à jour 2026-07-31 (Notion)** — Thomas a ajouté 2 nouvelles pages à l'export : **« Création d'une activité »** (refonte du formulaire de création en assistant multi-étapes, entièrement nouvelle — voir Lot I) et **« Paramètres »** (liste de champs texte, sans maquette — voir Lot J). La page « Tableau de bord de l'activité » a aussi été complétée (voir Lot A). Ça ajoute 1 nouvelle question ouverte (n°7, modèle de questions par activité) et 1 nouveau bug signalé (téléchargement du code d'intégration iframe qui produit un fichier 0 ko sur Mac). Aucune des 6 questions précédentes n'est résolue dans cet export.

**Mise à jour 2026-07-31 (infra)** — Migration complète de l'hébergement Azure → VPS OVH réalisée dans la foulée (voir section *Infra* ci-dessous) : les 2 items TO-DO « Passer à OVH » et « Créer un compte Brevo dédié » sont maintenant ✅ Fait ; la décision « Passer à Molly » a été **annulée** (on garde Stripe).

**Mise à jour 2026-07-31 (réponses)** — Olivier a tranché les questions 1 à 5 (détail dans les lots concernés) : paiements partiels/CPAS confirmés (Lot C), menu hamburger totalement supprimé (Lot A), numéro de ticket remis à 0 par activité (Lot D), définition de « Hors bilan » précisée (Lot D), périmètre de l'auto-proposition mail Excursion clarifié (Lot F). Il ne reste que les questions 6 (attestations fiscales) et 7 (modèle de questions par activité).

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
- 🐛 **Bug signalé, non investigué (2026-07-31, capture `09.54.00` annotée)** — Thomas : *« ça ne marche pas pour le moment, sur Mac, ça télécharge un fichier Register de 0ko »*, sur l'écran final « Création d'une activité » (code d'intégration iframe / bouton de téléchargement). À reproduire et diagnostiquer.

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
- ✅ **Fait** — Page d'accueil réduite à la liste « Activités récentes » (4 cartes stats, inscriptions récentes, actions rapides retirées, `HomeController` simplifié en conséquence).
- ✅ **Fait** — Menu du haut (dans une activité) réduit à « Tableau de bord » + dropdown « Pages spéciales » (liste des groupes, total des présences).
- ⏸️ **Pas fait (réponse Olivier, 2026-07-31)** — Menu hamburger **supprimé totalement** : tous ses liens (Contacts, Importer des parents/enfants, Équipe compris) redescendent en dessous sur la page d'accueil, dans la section « Paramètres généraux » (comme les screenshots l'illustrent, capture `18.38.25`). Plus de barre latérale globale du tout.
- ⏸️ **Pas fait (précision 2026-07-31, capture `10.35.33` annotée en vert)** — Le tableau de bord d'activité doit revenir à **8 grosses tuiles** (comme la maquette d'origine `18.09.45`) : transformer le bouton séparé « Paramètres de l'activité » en 8ᵉ tuile (à la place de la case actuellement vide), et ne garder en dessous que le bouton « Sortir de cette activité » (capture `10.03.21`). L'app actuelle a régressé vers 7 tuiles + 2 boutons séparés — c'est un retour en arrière à corriger, pas une nouvelle demande.

## Lot B — Confirmation des inscriptions

- ✅ Déjà le cas : ne montre que les inscriptions « à confirmer ».
- ✅ **Fait** — Clic sur le nom de l'enfant → fiche `Bookings/Details` (voir Lot C).
- ⏸️ **Pas fait, volontairement mis de côté** — Le coordinateur encode le montant à payer → mail au parent avec lien de paiement. Sensible (touche Stripe/facturation), à reprendre dans une session dédiée.

## Lot C — Présences

- ✅ **Fait** — Clic sur un enfant → fiche `Bookings/Details` enrichie (adresse/parent éditables, groupe, fiche médicale, historique des paiements).
- ✅ Confirmé dans le code : le filtre jour est déjà scopé à l'activité + jour sélectionné.
- ✅ **Fait** — Colonne « Payé » (✓/✗ + solde) dans `Presences.cshtml`.
- ⏸️ **Pas fait (réponse Olivier, 2026-07-31)** — Forcer une inscription non payée : le coordinateur doit pouvoir confirmer une inscription malgré un solde dû, puis encoder un ou plusieurs paiements manuels pour cette réservation, avec le **solde restant qui se met à jour** au fil des paiements encodés.
- ✅ **Fait** — Pages spéciales, enrichies au niveau demandé par Thomas :
  - **Liste des groupes imprimable** (`Groups`/`PrintGroups`) : sélection **multiple** de groupes (au lieu d'un seul à la fois, `<select multiple>`), options à cocher **Prévus/Présent/Signature** pour choisir les colonnes affichées, colonne **Signature** vide (émargement papier), **export PDF** et **export Excel** (`ExportGroupsPdf`/`ExportGroupsExcel`, réutilisent `IExportFacadeService` déjà utilisé ailleurs) en plus de l'impression navigateur existante. Rappel : les libellés « 3-4, 5-6, 7-8… » des captures sont juste les groupes de l'activité (comme nos « Groupe Rouge/Bleu/Vert »), pas une tranche d'âge — rien changé côté structure des groupes.
  - **Fait — « Imprimer tous les groupes du jour »** en un clic : bouton dédié sur `Groups.cshtml` (visible seulement si l'activité a un jour programmé aujourd'hui), distinct de l'impression filtrée groupe par groupe.
  - ✅ **Fait** — Total des présences journalières (`PresenceSummary`) : décomposé par indicateur ONE (Milieu défavorisé / Handicap léger / Handicap lourd) en plus du total brut réservé/présent (réf. capture `18.48.14`).

## Lot D — Comptes / Finances

- ⏸️ **Pas fait** — Simplification du parcours Comptes → Transactions : la cible (captures `18.48.58 1`/`18.50.34`) est de sauter directement sur la liste des transactions nue (sans les cartes stats ni les onglets de filtre) en cliquant sur « Comptes ».
- ✅ **Fait** — Bouton « Masquer les montants » sur `Transactions.cshtml`.
- ⏸️ **Pas fait (réponse Olivier, 2026-07-31)** — Numéro de ticket unique par ligne : compteur **remis à 0 à chaque nouvelle activité** (pas global, pas par organisation). Révèle aussi un champ **« Caisse / Compte »** (cash vs bancaire) absent du modèle actuel.
- ✅ **Fait** — Fusion Ajouter un paiement / Ajouter une dépense (`Financial/AddTransaction`, onglets Paiement/Dépense) : `Payment` (clé = réservation) et `Expense` (clé = activité) n'ayant ni la même clé ni les mêmes champs, la fusion est un écran unique à onglets hébergeant les deux formulaires existants inchangés (POST vers `PaymentsController.Create` / `FinancialController.CreateExpense`). Les 2 boutons de `Transactions.cshtml` pointent maintenant vers cet écran unique.
- 🐛 **Bug corrigé** — Clé de session incohérente entre `PaymentsController` (`"FinancialActivityId"`) et `FinancialController` (`"Financial_ActivityId"`) : le filtre par activité de `SelectBooking` était un no-op en production (listait toutes les réservations impayées de l'org, pas seulement celles de l'activité courante).
- ⏸️ **Partiel (réponse Olivier, 2026-07-31)** — Catégories : `ExpenseCategory.IsIncome` (bool) + `Budget` ajoutés. La référence (`18.58.21`/`19.00.10`) montre en réalité 3 valeurs (Entrée/Sortie/**Hors bilan**) et un champ **« Lié à un enfant ? »** absent — à revoir. **« Hors bilan » précisé** : sert aux mouvements internes type transfert bancaire (ex. verser 1000 € de caisse à la banque) — une transaction marquée Hors bilan ne doit **pas** apparaître comme +1000/-1000 dans les totaux Entrées/Sorties du bilan, pour ne pas le gonfler artificiellement.
- ✅ Déjà 100% auto-calculé, vérifié dans le code : catégories Équipe (Sorties) et PAF (Entrées).
- ⏸️ **Pas fait** — Rapport plus détaillé (par catégorie avec Budget + section Hors bilan séparée, réf. `19.00.54`) ; version actuelle = juste 2 colonnes Entrées/Sorties.

## Lot E — E-mails

- ⏸️ **Pas fait** — Épurer l'UI de `SendEmail.cshtml` : retirer le panneau Informations et les boutons « Enregistrer comme modèle »/« Historique », alléger la checkbox « Un email par enfant », réduire le panneau Variables de personnalisation (réf. `19.12.49 1` annotée).
- ✅ **Fait** — 3 modèles verrouillés (Confirmation d'inscription, Rappel fiche médicale, Rappel paiement) : uniques par organisation, non créables/dupliquables/supprimables, plus jamais copiés par activité (migration de nettoyage des copies déjà existantes).
- ✅ Déjà le cas — modèles Excursion libres.
- ✅ **Fait** — Bouton « Envoyer » sur `EmailTemplates/Index` → ouvre `SendEmail` avec le modèle pré-chargé.

## Lot F — Excursions

- ✅ **Fait** — Formulaire Créer/Modifier : Heure début/fin et Type retirés de l'écran (champs cachés, valeurs préservées — pas supprimés du modèle). Nom/Description/Date/Coût/Groupes restent.
- ✅ **Fait** — Liste « Gérer les excursions » : colonnes Type et finances retirées.
- 🐛 **Bug corrigé** — `Excursions.SendEmail` n'envoyait jamais rien réellement ; corrigé.
- ⏸️ **Pas fait (réponse Olivier, 2026-07-31)** — Auto-proposition d'un mail « Excursion » à la programmation : cible clarifiée — les familles éligibles sont celles **déjà inscrites à l'activité (stage) parente**, mais **pas encore inscrites à cette excursion précise**. Nouveau type de destinataire à ajouter au système actuel (qui ne cible aujourd'hui que les enfants déjà inscrits à la cible visée elle-même).

## Lot G — Équipe

- ✅ **Fait** — Panneau « Membres disponibles » déplacé sous « Équipe assignée », replié par défaut.
- ✅ **Fait** — Présences équipe jour/jour (miroir du système enfants) : nouvelle entité `TeamMemberDay` (+ migration avec backfill des assignations existantes en présent, pour ne rien changer rétroactivement aux salaires déjà calculés), page `TeamPresences` (sélecteur de jour + case à cocher par membre, même mécanisme AJAX que `Presences`), dans le dropdown « Pages spéciales ». Les lignes de présence sont créées/supprimées automatiquement à l'assignation/retrait d'un membre et à l'activation/désactivation d'un jour (formulaire d'édition, éditeur AJAX +/- jour, changement de plage de dates). Le calcul salarial (`FinancialCalculationService`, `FinancialController` Index/TeamSalaries/ExportTeamSalaries/Report) utilise désormais le nombre réel de jours cochés « présent » par membre au lieu de supposer 100% des jours de l'activité.
- ✅ Déjà satisfait, vérifié : compléments/dépenses par membre (`Expense.TeamMemberId`), décompte total par personne (`TeamSalaries.cshtml`).
- ✅ **Fait** — Stockage de l'extrait de casier judiciaire (`TeamMember.CriminalRecordUrl`).

## Lot H — ONE (organisme officiel)

- ✅ **Fait** — 4 tableaux par activité (`ActivityManagement/OneReport`) : listings 2-5 ans / 6 ans et plus (N°, nom, âge, dates, jours, prix payé, indicateurs) + présences hebdomadaires par tranche d'âge. Format calqué sur [`17.pdf`](17.pdf). Aucune migration nécessaire, testé (`OneReportTests.cs`). **Ce rapport reste par activité** (c'est un rapport officiel envoyé à l'ONE, distinct de l'attestation fiscale ci-dessous — à ne pas confondre).
- ⏸️ **Bloqué — en attente d'un exemple de Thomas (question n°1)** — Attestations fiscales : **regroupées par association**, pas par activité (confirmation du texte original — l'attestation fiscale donnée au parent est un document différent du rapport ONE par activité ci-dessus). Aucune attestation fiscale n'existe encore dans le code. Contrairement au rapport ONE (qui avait `17.pdf` comme référence exacte), on n'a **aucun exemple de mise en page/contenu réel** pour ce document officiel (mentions légales, montant déductible, période, etc.) — **à demander à Thomas avant de coder**, pour éviter de produire un document qui ne serait pas valable fiscalement.

## Lot I — Création d'une activité (refonte wizard, nouveau 2026-07-31)

Demande de refonte du formulaire de création d'activité : actuellement un formulaire plat unique (Titre/Du/Au), Thomas veut un **assistant multi-étapes avec jauge de progression** (maquette générique `10.21.29`, pastilles 1-2-3-4). Détail des 7 étapes :

- ⏸️ **Pas fait** — **Écran d'entrée** : page « Sélectionnez votre activité » avec gros boutons par stage + « Créer une nouvelle activité » (vert) + « Déconnexion » (rouge) — réutilise le même écran que « Page d'accueil générale organisation ».
- ⏸️ **Pas fait** — **Étape 1** : Titre + Dates (inchangé dans le principe, juste repositionné dans le wizard).
- ⏸️ **Pas fait** — **Étape 2 — Paramétrage des dates** : supporter 2 modes d'inscription, « au jour le jour » et « à la semaine » (le lundi représente visuellement toute la semaine groupée). **À supprimer** (annoté en rouge, capture `09.19.42`) : boutons « Ajouter un jour avant/après », « Retirer le 1er/dernier jour », bandeau « Gérez les jours actifs ». **À la place** : un bouton « Ajouter une date » ouvrant un calendrier, en gardant la suppression ligne par ligne (icônes crayon/poubelle déjà présentes, capture `10.25.06`).
- ✅ **Jugé conforme tel quel** — **Étape 3 — Règlement (R.O.I.)** : upload PDF + texte de case à cocher (capture `09.31.41`), juste à intégrer dans le wizard.
- ✅ **Jugé conforme tel quel** — **Étape 4 — Limitations** : codes postaux autorisés/refusés (vide = tous), nombre max d'enfants/jour + message « COMPLET » personnalisable (capture `aa8a0a57`), juste à intégrer dans le wizard.
- ⏸️ **Bloqué par la question n°2** — **Étape 5 — Autres questions** : reprend les questions personnalisées par activité, avec la question du modèle org→activité (n°2). **À supprimer** (annoté en rouge, capture `09.49.21`) : le toggle « Actif » du formulaire de question (« si on pose la question pour l'activité, c'est que c'est actif »).
- ✅ **Jugé conforme tel quel** — **Étape 6 — Affichage** : dates d'affichage du formulaire, message si aucun formulaire actif, page de redirection après envoi (capture `09.50.34`), juste à intégrer dans le wizard.
- ✅ **Jugé « super » tel quel** — **Étape 7 — Final** : couleurs (fond/boutons), URL directe, code d'intégration iframe avec aperçu (capture `09.54.00`). 🐛 **Bug signalé** : le téléchargement produit un fichier « Register » de 0 ko sur Mac — voir TO-DO.

## Lot J — Paramètres (nouveau 2026-07-31, sans maquette)

Page listée dans l'export mais sans capture d'écran associée — juste une liste de champs à formaliser :

- ⏸️ **À maquetter** — **Signalétique de l'activité** : Logo, Titre, Adresse, E-mail (reply-to des mails envoyés), Téléphone 1, Téléphone 2, Numéro de compte, Numéro d'entreprise, Nom du responsable, Signature du responsable. Reprise par défaut des paramètres généraux de l'organisation, adaptable par activité.
- ⏸️ **À maquetter** — Dates de l'activité (probablement un renvoi vers l'Étape 2 du wizard de création, Lot I).
- ⏸️ **À maquetter** — Groupes de l'activité.
- ⏸️ **À maquetter** — Formulaire (probablement un renvoi vers les Étapes 5/6 du wizard, Lot I).

---

## Ordre proposé

Lots C, D (sauf numéro de ticket/Hors bilan/Rapport détaillé), E et G sont livrés. Toutes les questions 1 à 5 d'origine étant désormais tranchées par Olivier, **plus aucun item n'est bloqué en attente d'une réponse** sauf Lot H (attestations fiscales) et une partie de Lot I (modèle de questions). Reste à coder :

1. **Lot A** — suppression du menu hamburger (liens redescendus sur la page d'accueil) ; retour à 8 tuiles sur le tableau de bord d'activité. Les deux sont maintenant des specs claires, codables directement.
2. **Lot C** — forcer une inscription non payée + paiements manuels multiples avec solde qui se met à jour.
3. **Lot D** — numéro de ticket (remis à 0 par activité), Hors bilan (exclu des totaux Entrées/Sorties), rapport détaillé par catégorie.
4. **Lot F** — auto-proposition mail Excursion (nouveau type de destinataire : inscrit à l'activité, pas encore à l'excursion).
5. **Lot H** — attestations fiscales par association : bloqué en attente d'un exemple de Thomas (question n°1).
6. **Lot I** — wizard de création d'activité : étapes 1/2/3/4/6/7 codables directement (réagencement + retrait des boutons de dates + nouveau bouton calendrier), étape 5 bloquée par la question n°2 (modèle de questions). Bug Mac (fichier 0 ko) à investiguer indépendamment.
7. **Lot J** — Paramètres : à maquetter avec Thomas avant de coder (aucune capture fournie).

Codable sans attendre Thomas : Lot A en entier, Lot C, Lot D, Lot F, et Lot I sauf l'étape 5. Bloqué par Thomas : Lot H (question n°1), l'étape 5 de Lot I (question n°2), et Lot J (maquette manquante).
