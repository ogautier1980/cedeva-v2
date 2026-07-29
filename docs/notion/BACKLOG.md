# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier + exemple [`17.pdf`](17.pdf) (listing ONE).

Ce document restructure le retour brut de Thomas (écran par écran, en vrac) en lots exploitables. Aucun développement n'a démarré — c'est un plan de travail à valider avant d'attaquer.

---

## ⚠️ Questions ouvertes à trancher avant de commencer

1. **Paiements partiels / CPAS / facture** — le coordinateur doit-il pouvoir forcer une inscription non payée et encoder un paiement manuel ? Quelles règles (échéancier, tiers payant) ?
2. **`+Ajouter un paiement` / `+Ajouter une dépense`** — Thomas demande d'abord une explication du fonctionnement actuel avant de fusionner les deux écrans.
3. **Envoi de mail depuis un modèle** — Thomas ne trouve pas ce parcours dans l'UI actuelle ; à vérifier si la feature existe et est juste mal exposée, ou si elle manque.
4. **Attestations fiscales ONE** — par activité (actuel) ou regroupées par association ?
5. **⚠️ Conflit potentiel avec le Lot 4 déjà livré** (voir mémoire `cedeva-roadmap-2026-06`) : `EmailTemplate.ActivityId` (nullable) a été introduit pour distinguer bibliothèque org vs templates copiés par activité, avec copie automatique à la création de l'activité. Le retour Notion (Lot E ci-dessous) demande au contraire que les 3 modèles génériques restent **uniques et partagés par toutes les activités**, sans duplication. Ces deux visions sont contradictoires — à clarifier avec Thomas avant de toucher au Lot E.
6. **Menu hamburger (barre latérale globale)** — Thomas veut le sortir des pages internes pour ne le garder qu'en page d'accueil. Contrairement au menu du haut (scope = pages d'une activité, déjà simplifié), le hamburger est la nav principale de **toute l'app** (Dashboard, Activities, Bookings, Children, Parents, TeamMembers, Contacts, EmailTemplates, ExpenseCategories, Import…). Le retirer des pages internes prive l'utilisateur de tout accès à ces écrans depuis l'intérieur d'une activité — à clarifier : où ces liens doivent-ils vivre à la place (un menu secondaire ? uniquement accessible en revenant à l'accueil) ?

---

## Lot A — Accueil & Navigation (fondation, prioritaire)

Tout le reste s'appuie sur cette navigation ; à faire en premier.

- ✅ **Fait (2026-07-29)** — Tableau de bord d'activité : logique inversée dans `Home/Index.cshtml` — clic sur le **titre** de l'activité → `ActivityManagement/Index` (les 7 gros boutons) ; clic sur le bouton (renommé **« Paramètres »**, resx `Home.Manage`, FR/EN/NL) → `/Activities/Details` (paramètres de l'activité).
- Page d'accueil réduite à la seule liste des stages (retirer le superflu du menu hamburger). — **pas encore fait**, nécessite de redéfinir ce qu'est la « page d'accueil la plus simple » (`Home/Index` actuel est un dashboard avec stats, pas juste une liste de stages).
- Clic sur un stage → écran détail/tableau de bord de l'activité. — déjà le cas via le changement ci-dessus.
- ✅ **Fait (2026-07-29)** — Menu du haut (dans une activité) : `_Layout.cshtml` ne garde plus que **« Tableau de bord »** (retour aux 7 boutons) + un dropdown **« Pages spéciales »** (Liste des groupes, Total des présences). Les anciens raccourcis directs (Inscriptions, Présences, Comptes, E-mails, Excursions, Équipe, ONE) ont été retirés du haut — ils restent accessibles via les 7 boutons ; pour Comptes/E-mails dont les sous-pages (Transactions/TeamSalaries/Report, SentEmails/EmailTemplates) n'étaient QUE dans ce dropdown, des liens de remplacement ont été ajoutés en page (Financial/Index avait déjà ces liens ; ajoutés sur `SendEmail.cshtml`).
- ⏸️ **Non traité** — Menu hamburger (barre latérale globale : Dashboard/Activities/Bookings/Children/Parents/TeamMembers/Contacts/…) : le retrait envisagé par Thomas ("uniquement en page d'accueil") a une portée bien plus large que le menu du haut — il s'agit de la navigation principale de **toute l'application**, pas seulement des pages d'activité. Non fait par prudence : à clarifier avant d'y toucher (question ouverte n°6 ci-dessous).

## Lot B — Confirmation des inscriptions

- N'afficher que les inscriptions **« À confirmer »** — déjà le cas (`UnconfirmedBookings.cshtml` ne liste que `!IsConfirmed`).
- ✅ **Fait (2026-07-29)** — Clic sur le nom de l'enfant (UnconfirmedBookings, ManageBookings, Présences) → `Bookings/Details` (fiche enrichie, voir Lot C).
- Le coordinateur encode le montant à payer → déclenche un mail au parent (récap + lien de paiement). — **pas encore fait** (sensible : touche facturation + Stripe, mis de côté volontairement cette session).

## Lot C — Présences

- ✅ **Fait (2026-07-29)** — Clic sur un enfant dans la liste de présence → fiche `Bookings/Details`, enrichie avec : adresse + email + téléphone du parent (lecture + lien « Modifier les coordonnées » vers `Parents/Edit`), groupe (déjà présent), fiche médicale (déjà présente), **historique des paiements** (liste `Booking.Payments` + bouton « Ajouter un paiement »). Pas de migration nécessaire (toutes les données existaient déjà en base).
- Vérifier que le filtre jour (par défaut « aujourd'hui ») liste bien les enfants inscrits à **cette activité précise**. — confirmé dans le code (`BuildChildrenList`/`SelectDefaultActivityDay`, `ActivityManagementController.cs:248-301`) : filtre déjà scopé à l'activité + jour sélectionné.
- ✅ **Fait (2026-07-29)** — Colonne **« Payé »** (✓ vert / ✗ rouge + solde restant) ajoutée dans `Presences.cshtml`, alimentée par `Booking.TotalAmount`/`PaidAmount` (déjà existants, aucune migration nécessaire).
- Dépend de la question ouverte n°1 (paiement manuel/forcé, échelonnement, CPAS) pour la partie « forcer une inscription non payée » — non traité.
- ✅ **Fait (2026-07-29)** — Pages spéciales (accessibles depuis le dropdown « Pages spéciales » du menu du Lot A) :
  - **Liste des groupes** (`ActivityManagement/Groups` + `PrintGroups`), filtrable par groupe et par jour, imprimable (même gabarit que la fiche de présence existante).
  - **Total des présences journalières** (`ActivityManagement/PresenceSummary`) : réservés/présents agrégés par jour.

## Lot D — Comptes / Finances

- ✅ **Fait (2026-07-29)** — Liste des transactions : bouton **« Masquer les montants »** sur `Financial/Transactions.cshtml` (flou CSS + préférence retenue en `localStorage`, aucun changement backend).
- Détail de ligne limité à la catégorie (rien de plus). — déjà globalement le cas (colonne « Détails » = catégorie/excursion/assigné), non retouché.
- Chaque ligne porte un **ID « numéro de ticket »** unique, pour classer les tickets papier dans une farde avec une numérotation correspondante. — **pas encore fait** : ni `Payment` ni `Expense` n'ont de numéro séquentiel aujourd'hui (`StructuredCommunication`/`Reference` existent mais ne sont pas ça) ; nécessite une migration + un choix de schéma de numérotation (par activité ? par organisation ? remise à zéro annuelle ?) à trancher avec Thomas avant de coder.
- Fusionner les écrans **« Ajouter un paiement »** et **« Ajouter une dépense »** en un seul écran (après clarification, question ouverte n°2). — non traité, toujours bloqué par la question ouverte.
- ✅ **Fait (2026-07-29, partiel)** — Catégories : `ExpenseCategory` a maintenant un champ **`IsIncome`** (Entrée/Sortie) et un **`Budget`** optionnel (migration `AddExpenseCategoryTypeAndBudget`), éditables sur Create/Edit, affichés sur l'Index. ⚠️ Limite connue : les `Payment` (Entrées/PAF) n'ont toujours pas de notion de catégorie du tout aujourd'hui — seuls les `Expense` (Sorties) en ont une. Le champ `IsIncome` permet donc de *classer de futures catégories* mais ne relie pas encore les paiements existants à une catégorie « Entrée » — à voir avec Thomas si c'est nécessaire ou si l'auto-calcul PAF (déjà fait, voir plus bas) suffit.
- Bilan présenté en 2 grandes catégories : **Entrées** / **Sorties**, plus un statut **« Hors bilan »** (ex. transfert compte à vue → épargne : crée une ligne dans les comptes mais n'impacte pas le bilan). — **pas encore fait** : aucune notion de « hors bilan » n'existe ; toucherait le calcul du bilan (`FinancialCalculationService`) à plusieurs endroits, à cadrer avant de coder.
- ✅ **Déjà satisfait, vérifié (2026-07-29)** — Sous-catégories « Équipe » (Sorties) et « PAF » (Entrées) : **déjà 100% auto-calculées** aujourd'hui (`FinancialCalculationService.CalculateTeamMemberSalary` pour Équipe, agrégation des `Payment.Amount` pour PAF) — aucune saisie manuelle, rien à coder. Confirmé en lisant le code, pas juste supposé.
- Clic sur une sous-catégorie → liste des tickets liés à cette catégorie. — non retouché (dépend du numéro de ticket ci-dessus).
- ✅ **Fait (2026-07-29)** — Rapport (`Financial/Report.cshtml`) restructuré en 2 colonnes **Entrées** (vert) / **Sorties** (rouge, regroupant dépenses org + salaires équipe), même code couleur que la page Transactions. Pure présentation, aucune donnée ni logique modifiée.

## Lot E — E-mails

*(à démarrer seulement après résolution du conflit noté en question ouverte n°5)*

- Épurer l'UI de la page d'envoi de mail.
- Ne garder que **3 modèles génériques verrouillés** : Confirmation d'inscription, Rappel fiche médicale, Rappel paiement — modifiables mais **non remplaçables/dupliquables** (éviter les doublons type « Confirmation de réservation »).
- Modèles génériques disponibles **dans toutes les activités**, sans duplication par activité.
- Modèles **Excursion** : libres — création/modification/suppression sans restriction.
- Retrouver ou documenter le parcours d'envoi d'un mail à partir d'un modèle (question ouverte n°3).

## Lot F — Excursions

- Formulaire « Gérer les excursions » réduit au strict minimum : **Titre, date, groupe**. — non traité (le formulaire actuel a déjà plus de champs : Description, horaires, coût, type, groupes — aucun n'a été retiré, pas de décision prise sur lesquels cacher).
- Ne pas supprimer les champs actuels du modèle — les conserver en base pour une réintroduction ultérieure (Thomas prévoit de les redemander).
- 🐛 **Bug trouvé et corrigé (2026-07-29)** — `Excursions.SendEmail` (POST) ne faisait qu'afficher « X emails envoyés » sans jamais réellement envoyer quoi que ce soit (aucun appel à un service d'email). Corrigé : envoie vraiment maintenant (variables fusionnées par enfant ou brut par parent, pièce jointe incluse), comme le fait déjà `ActivityManagement.SendEmail`.
- Quand une excursion est programmée : proposer automatiquement l'envoi d'un mail « Excursion ». — **pas fait, et plus compliqué que prévu** : les options de destinataires actuelles (« tous les inscrits », « groupe X inscrits ») ne fonctionnent QUE pour des enfants déjà inscrits à l'excursion. Juste après la création, personne n'est encore inscrit → rediriger automatiquement vers l'écran d'envoi tomberait sur « Aucun destinataire trouvé ». Ce que Thomas décrit (annoncer la nouvelle excursion aux familles éligibles, même non encore inscrites) est un destinataire différent, pas encore supporté — à cadrer avant de coder l'auto-proposition.

## Lot G — Équipe

- Alléger la vue principale ; déplacer l'historique en dessous (sans le supprimer). — non traité.
- Ajouter des **présences équipe jour par jour**, à cocher par le coordinateur (miroir du système de présences enfants). — non traité, gros morceau : nécessite une nouvelle entité (type `TeamMemberDay`) + migration + UI, ET changerait le calcul salarial actuel (qui suppose aujourd'hui une présence à 100% de tous les jours de l'activité) → impact financier réel, à valider avec Thomas avant de coder.
- Fiche détail d'un membre : compléments/dépenses. — ✅ **déjà satisfait**, vérifié : `Expense.TeamMemberId` + `ExpenseType` (Reimbursement/PersonalConsumption) existent déjà et alimentent le calcul salarial. Rien à coder.
- ✅ **Fait (2026-07-29)** — Stockage de l'**extrait de casier judiciaire** sur la fiche membre (`TeamMember.CriminalRecordUrl`, migration `AddTeamMemberCriminalRecordUrl`), upload/consultation/suppression calqués exactement sur le mécanisme déjà existant pour le brevet (`LicenseUrl`).
- Vue consolidée « décompte total à payer par personne ». — ✅ **déjà satisfait**, vérifié : `Financial/TeamSalaries.cshtml` + export Excel existent déjà avec le total par personne. Rien à coder (scope actuel = par activité, pas cumulé toutes activités/année — à voir si c'est ce qui est demandé).

## Lot H — ONE (organisme officiel)

- ✅ **Fait (2026-07-29)** — Par activité, 4 tableaux générés (`ActivityManagement/OneReport`, accessible depuis la carte « ONE » du tableau de bord) :
  1. Listing enfants **2 à 5 ans** : N°, nom et prénom, âge, date arrivée/départ, nb jours, prix payé, milieu défavorisé, handicap léger, handicap lourd
  2. Listing enfants **6 ans et plus** : mêmes colonnes
  3. Présences **2 à 5 ans** par semaine/jour
  4. Présences **6 ans et plus** par semaine/jour
  - Format calqué précisément sur [`17.pdf`](17.pdf) (colonnes, ordre, cases X pour les indicateurs). Âge calculé par rapport à la **date de début de l'activité** (pas « aujourd'hui »), donc stable dans le temps.
  - Aucune migration nécessaire : les champs `Child.IsDisadvantagedEnvironment`/`IsMildDisability`/`IsSevereDisability` existaient déjà.
  - Couvert par `OneReportTests.cs` (bucket d'âge, montant payé, indicateurs).
- Attestations fiscales : à trancher (question ouverte n°4) — par activité ou regroupées par association. Non traité (indépendant des 4 tableaux ci-dessus).

---

## Ordre proposé

1. **Lot A** (navigation) — fondation dont dépendent tous les écrans suivants.
2. **Lot C** (présences) + **Lot B** (confirmation inscriptions) — cœur du quotidien coordinateur, forte valeur perçue.
3. **Lot D** (comptes) — gros morceau, à découper en sous-lots si besoin (D1 liste/tickets, D2 catégories/budget, D3 auto-calcul Équipe/PAF, D4 rapport).
4. **Lot G** (équipe) et **Lot F** (excursions) — périmètre plus contenu.
5. **Lot E** (e-mails) — après clarification du conflit avec le Lot 4 existant.
6. **Lot H** (ONE) — dépend d'une décision externe (attestations), peut être traité en parallèle sur les tableaux seuls.

Chaque lot doit démarrer par une session de clarification des questions ouvertes qui le concernent, avant tout code.
