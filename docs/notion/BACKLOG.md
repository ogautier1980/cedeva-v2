# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier (certaines annotées par Thomas en rouge/vert) + exemple [`17.pdf`](17.pdf) (listing ONE).

**Mise à jour 2026-07-30** — 5 des 10 questions posées après la relecture complète ont été tranchées par le user : attestations fiscales (par association), présences équipe (confirmé), richesse de la liste des groupes (confirmé, avec une précision), impression groupée des groupes (confirmé), ventilation ONE des présences (confirmé). Les items confirmés et codables sans blocage ont été livrés (Lots C, D, E). Une nouvelle question est apparue en construisant Lot H (besoin d'un exemple réel d'attestation fiscale) : il reste 6 questions ouvertes pour Thomas.

---

## ⚠️ Questions ouvertes pour Thomas

1. **Paiements partiels / CPAS / facture** — le coordinateur doit-il pouvoir forcer une inscription non payée et encoder un paiement manuel ? Quelles règles (échéancier, tiers payant) ?
2. **Menu hamburger** — la cible est connue (contenu redescendu en section « Paramètres généraux » sur la page d'accueil, capture `18.38.25`), mais où vont précisément Contacts, Importer des parents/enfants et Équipe (pas montrés dans le mockup) ?
3. **Numéro de ticket** (Lot D) — quel schéma de numérotation ? (par activité, par organisation, remise à zéro annuelle ou jamais). La référence (capture `19.02.39`) suggère un compteur global jamais remis à zéro.
4. **« Hors bilan »** (Lot D) — au-delà du principe (3e valeur du champ Type, confirmé par la référence), quelles transactions concrètes doivent y aller précisément ?
5. **Auto-proposition mail Excursion** (Lot F) — Thomas veut annoncer une nouvelle excursion aux familles éligibles **avant même qu'elles soient inscrites** ; ça n'existe pas dans le système de destinataires actuel (qui ne cible que des enfants déjà inscrits). Un nouveau type de destinataire est à concevoir avec Thomas.
6. **Attestations fiscales** (Lot H) — besoin d'un exemple réel (mise en page, mentions légales obligatoires, montant déductible, période couverte…), comme `17.pdf` l'a été pour le rapport ONE. Sans ça, le risque est de construire un document non valable fiscalement.

*(Résolues et retirées de cette liste — détail dans le lot concerné : explication paiement/dépense → Lot D ; envoi depuis un modèle et conflit Lot 4 → Lot E ; attestations fiscales, présences équipe, richesse des groupes, impression groupée, ventilation ONE → voir Lots C/G/H ci-dessous.)*

## 📌 TO-DO (hors backlog UX)

- **Passer à OVH (VPS-2)** — migration d'hébergement (actuellement Azure). Implique une **migration vers PostgreSQL** (actuellement SQL Server).
- **Passer à Molly** — remplace Stripe comme solution de paiement en ligne (actuellement `StripePaymentGateway`, voir [ADR 0010](../adr/0010-online-payments-provider-agnostic-stripe.md)).
- **Créer un compte Brevo dédié pour Cedeva** — la config existe (`Brevo:ApiKey`/`SenderEmail: noreply@cedeva.be`/`SenderName: Cedeva` dans `appsettings.json`) mais la clé API est vide : il faut créer le compte Brevo propre à Cedeva et renseigner sa clé (`Brevo__ApiKey` en config Azure) pour que l'envoi d'emails (confirmations, rappels, SendEmail...) fonctionne réellement en production.

---

## Lot A — Accueil & Navigation

- ✅ **Fait** — Tableau de bord d'activité : clic sur le **titre** → 7 gros boutons (`ActivityManagement/Index`) ; clic sur **« Paramètres »** (renommé, ex-« Gérer ») → réglages de l'activité (`Activities/Details`).
- ✅ **Fait** — Page d'accueil réduite à la liste « Activités récentes » (4 cartes stats, inscriptions récentes, actions rapides retirées, `HomeController` simplifié en conséquence).
- ✅ **Fait** — Menu du haut (dans une activité) réduit à « Tableau de bord » + dropdown « Pages spéciales » (liste des groupes, total des présences).
- ⏸️ **Non traité** — Menu hamburger (barre latérale globale) : cible connue mais reste à cadrer, voir question n°2.

## Lot B — Confirmation des inscriptions

- ✅ Déjà le cas : ne montre que les inscriptions « à confirmer ».
- ✅ **Fait** — Clic sur le nom de l'enfant → fiche `Bookings/Details` (voir Lot C).
- ⏸️ **Pas fait, volontairement mis de côté** — Le coordinateur encode le montant à payer → mail au parent avec lien de paiement. Sensible (touche Stripe/facturation), à reprendre dans une session dédiée.

## Lot C — Présences

- ✅ **Fait** — Clic sur un enfant → fiche `Bookings/Details` enrichie (adresse/parent éditables, groupe, fiche médicale, historique des paiements).
- ✅ Confirmé dans le code : le filtre jour est déjà scopé à l'activité + jour sélectionné.
- ✅ **Fait** — Colonne « Payé » (✓/✗ + solde) dans `Presences.cshtml`.
- ⏸️ La partie « forcer une inscription non payée » dépend de la question n°1.
- ✅ **Fait** — Pages spéciales, enrichies au niveau demandé par Thomas :
  - **Liste des groupes imprimable** (`Groups`/`PrintGroups`) : sélection **multiple** de groupes (au lieu d'un seul à la fois, `<select multiple>`), options à cocher **Prévus/Présent/Signature** pour choisir les colonnes affichées, colonne **Signature** vide (émargement papier), **export PDF** et **export Excel** (`ExportGroupsPdf`/`ExportGroupsExcel`, réutilisent `IExportFacadeService` déjà utilisé ailleurs) en plus de l'impression navigateur existante. Rappel : les libellés « 3-4, 5-6, 7-8… » des captures sont juste les groupes de l'activité (comme nos « Groupe Rouge/Bleu/Vert »), pas une tranche d'âge — rien changé côté structure des groupes.
  - **Fait — « Imprimer tous les groupes du jour »** en un clic : bouton dédié sur `Groups.cshtml` (visible seulement si l'activité a un jour programmé aujourd'hui), distinct de l'impression filtrée groupe par groupe.
  - ✅ **Fait** — Total des présences journalières (`PresenceSummary`) : décomposé par indicateur ONE (Milieu défavorisé / Handicap léger / Handicap lourd) en plus du total brut réservé/présent (réf. capture `18.48.14`).

## Lot D — Comptes / Finances

- ⏸️ **Pas fait** — Simplification du parcours Comptes → Transactions : la cible (captures `18.48.58 1`/`18.50.34`) est de sauter directement sur la liste des transactions nue (sans les cartes stats ni les onglets de filtre) en cliquant sur « Comptes ».
- ✅ **Fait** — Bouton « Masquer les montants » sur `Transactions.cshtml`.
- ⏸️ **Pas fait** — Numéro de ticket unique par ligne (question n°3) ; révèle aussi un champ **« Caisse / Compte »** (cash vs bancaire) absent du modèle actuel.
- ✅ **Fait** — Fusion Ajouter un paiement / Ajouter une dépense (`Financial/AddTransaction`, onglets Paiement/Dépense) : `Payment` (clé = réservation) et `Expense` (clé = activité) n'ayant ni la même clé ni les mêmes champs, la fusion est un écran unique à onglets hébergeant les deux formulaires existants inchangés (POST vers `PaymentsController.Create` / `FinancialController.CreateExpense`). Les 2 boutons de `Transactions.cshtml` pointent maintenant vers cet écran unique.
- 🐛 **Bug corrigé** — Clé de session incohérente entre `PaymentsController` (`"FinancialActivityId"`) et `FinancialController` (`"Financial_ActivityId"`) : le filtre par activité de `SelectBooking` était un no-op en production (listait toutes les réservations impayées de l'org, pas seulement celles de l'activité courante).
- ⏸️ **Partiel** — Catégories : `ExpenseCategory.IsIncome` (bool) + `Budget` ajoutés. La référence (`18.58.21`/`19.00.10`) montre en réalité 3 valeurs (Entrée/Sortie/**Hors bilan**, question n°4) et un champ **« Lié à un enfant ? »** absent — à revoir.
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
- ⏸️ **Pas fait** — Auto-proposition d'un mail « Excursion » à la programmation : bloqué par la question n°5 (nouveau type de destinataire à concevoir).

## Lot G — Équipe

- ✅ **Fait** — Panneau « Membres disponibles » déplacé sous « Équipe assignée », replié par défaut.
- ✅ **Fait** — Présences équipe jour/jour (miroir du système enfants) : nouvelle entité `TeamMemberDay` (+ migration avec backfill des assignations existantes en présent, pour ne rien changer rétroactivement aux salaires déjà calculés), page `TeamPresences` (sélecteur de jour + case à cocher par membre, même mécanisme AJAX que `Presences`), dans le dropdown « Pages spéciales ». Les lignes de présence sont créées/supprimées automatiquement à l'assignation/retrait d'un membre et à l'activation/désactivation d'un jour (formulaire d'édition, éditeur AJAX +/- jour, changement de plage de dates). Le calcul salarial (`FinancialCalculationService`, `FinancialController` Index/TeamSalaries/ExportTeamSalaries/Report) utilise désormais le nombre réel de jours cochés « présent » par membre au lieu de supposer 100% des jours de l'activité.
- ✅ Déjà satisfait, vérifié : compléments/dépenses par membre (`Expense.TeamMemberId`), décompte total par personne (`TeamSalaries.cshtml`).
- ✅ **Fait** — Stockage de l'extrait de casier judiciaire (`TeamMember.CriminalRecordUrl`).

## Lot H — ONE (organisme officiel)

- ✅ **Fait** — 4 tableaux par activité (`ActivityManagement/OneReport`) : listings 2-5 ans / 6 ans et plus (N°, nom, âge, dates, jours, prix payé, indicateurs) + présences hebdomadaires par tranche d'âge. Format calqué sur [`17.pdf`](17.pdf). Aucune migration nécessaire, testé (`OneReportTests.cs`). **Ce rapport reste par activité** (c'est un rapport officiel envoyé à l'ONE, distinct de l'attestation fiscale ci-dessous — à ne pas confondre).
- ⏸️ **Bloqué — en attente d'un exemple de Thomas** — Attestations fiscales : **regroupées par association**, pas par activité (confirmation du texte original — l'attestation fiscale donnée au parent est un document différent du rapport ONE par activité ci-dessus). Aucune attestation fiscale n'existe encore dans le code. Contrairement au rapport ONE (qui avait `17.pdf` comme référence exacte), on n'a **aucun exemple de mise en page/contenu réel** pour ce document officiel (mentions légales, montant déductible, période, etc.) — **à demander à Thomas avant de coder**, pour éviter de produire un document qui ne serait pas valable fiscalement.

---

## Ordre proposé

Lots C, D (sauf numéro de ticket/Hors bilan/Rapport détaillé), E et G sont livrés. Reste :

1. **Lot A** — cadrer le hamburger (question n°2).
2. **Lot D** — numéro de ticket (question n°3), Hors bilan (question n°4), rapport détaillé par catégorie.
3. **Lot F** — auto-proposition mail Excursion (question n°5).
4. **Lot H** — attestations fiscales par association : bloqué en attente d'un exemple de Thomas (question n°6).

Tout ce qui reste dans ces 4 points dépend d'une réponse de Thomas (questions 2 à 6) ; rien d'autre n'est codable sans attendre.
