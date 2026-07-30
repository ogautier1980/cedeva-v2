# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier + exemple [`17.pdf`](17.pdf) (listing ONE).

Ce document restructure le retour brut de Thomas (écran par écran, en vrac) en lots exploitables. **Mise à jour 2026-07-30** : plusieurs lots ont déjà été traités (voir les statuts ✅/⏸️ dans chaque lot ci-dessous) ; ce qui reste est presque entièrement bloqué par une des questions ouvertes ou nécessite une décision produit de Thomas — voir la liste juste en dessous.

---

## ⚠️ Questions ouvertes à trancher avant de commencer

1. **Paiements partiels / CPAS / facture** — le coordinateur doit-il pouvoir forcer une inscription non payée et encoder un paiement manuel ? Quelles règles (échéancier, tiers payant) ?
2. **✅ Répondu (2026-07-30) — `+Ajouter un paiement` / `+Ajouter une dépense`** : ce sont deux écrans structurellement différents, pas juste deux variantes d'un même formulaire.
   - **Ajouter un paiement** (`PaymentsController.Create`) est toujours rattaché à une **réservation précise** (`Payment.BookingId` obligatoire) : enfant, parent, activité, montant déjà payé sont affichés en lecture seule. À la validation, il met à jour `Booking.PaidAmount`/`PaymentStatus`. Champs : Montant, Date, Moyen de paiement (Cash/Autre), Référence.
   - **Ajouter une dépense** (`FinancialController.CreateExpense`) est rattaché à une **activité entière** (`Expense.ActivityId` obligatoire), jamais à un enfant/une réservation. Champs : Libellé (obligatoire), Description, Montant, Date, Catégorie (texte libre avec suggestions), Assigné à (membre d'équipe ou "caisse/carte organisation" — avec un type Remboursement/Consommation perso qui n'existe pas côté paiement).
   - **Conclusion** : les deux entités n'ont ni la même clé obligatoire (réservation vs activité), ni le même jeu de champs (`Payment` a Moyen de paiement/Statut, `Expense` a Libellé/Catégorie/Assigné à) — les fusionner en un seul écran est possible techniquement (bouton bascule Entrée/Sortie) mais ferait cohabiter deux formulaires largement indépendants derrière un même écran, pas un vrai formulaire unique. **Décision à prendre avec Thomas** : garder les deux écrans séparés (mais améliorer leurs points d'entrée/labels), ou construire cet écran à bascule malgré la duplication de champs qu'il implique.
3. **✅ Répondu (2026-07-30) — Envoi de mail depuis un modèle** : la fonctionnalité **existe déjà entièrement**, mais uniquement à l'intérieur de l'écran d'envoi (`ActivityManagement/SendEmail`) : un menu déroulant + bouton « Charger le modèle » y remplit automatiquement Objet et Message par AJAX. **Ce qui manque** : aucun raccourci dans l'autre sens — la liste des modèles (`EmailTemplates/Index`) n'a pas de bouton « Envoyer »/« Utiliser ce modèle » qui ramènerait vers l'écran d'envoi avec le modèle pré-sélectionné ; c'est très probablement pour ça que Thomas ne l'a pas trouvé. Correctif simple si souhaité : ajouter ce bouton sur `EmailTemplates/Index.cshtml`.
4. **✅ Décision (2026-07-30) — Attestations fiscales ONE** : **par activité**, pas regroupées par association. Reste à construire (aucune attestation fiscale n'existe encore dans le code aujourd'hui) — voir Lot H.
5. **✅ Décision (2026-07-30) — Conflit avec le Lot 4 déjà livré** : le Lot 4 (voir mémoire `cedeva-roadmap-2026-06`) a introduit `EmailTemplate.ActivityId` (nullable) pour distinguer bibliothèque org vs templates copiés par activité, avec copie automatique à la création de l'activité. Ça contredit la demande de Thomas (Lot E) que les 3 modèles génériques restent **uniques et partagés par toutes les activités**, sans duplication. **On tranche en faveur de la demande de Thomas, pas de l'architecture déjà livrée** : le Lot 4 devra être adapté quand on attaquera le Lot E — au minimum, les 3 types verrouillés (Confirmation d'inscription, Rappel fiche médicale, Rappel paiement) doivent redevenir des modèles uniques au niveau organisation, sans copie par activité ni possibilité de duplication ; les modèles **Excursion** restent libres et peuvent continuer à fonctionner par activité comme aujourd'hui. Le détail de l'implémentation (retirer la copie automatique pour ces 3 types, migrer les copies existantes déjà créées en base) reste à faire lors du Lot E.
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

*(débloqué — voir décision en question ouverte n°5 : on suit la demande de Thomas, le Lot 4 sera adapté en conséquence)*

- Épurer l'UI de la page d'envoi de mail.
- Ne garder que **3 modèles génériques verrouillés** : Confirmation d'inscription, Rappel fiche médicale, Rappel paiement — modifiables mais **non remplaçables/dupliquables** (éviter les doublons type « Confirmation de réservation »). Nécessite d'adapter le Lot 4 (retirer la copie par activité pour ces 3 types précis, voir question n°5).
- Modèles génériques disponibles **dans toutes les activités**, sans duplication par activité.
- Modèles **Excursion** : libres — création/modification/suppression sans restriction (déjà le cas, compatible avec le Lot 4 existant).
- ✅ **Répondu (2026-07-30, question n°3)** — le parcours existe déjà (menu déroulant + auto-remplissage dans `SendEmail.cshtml`) ; il manque juste un raccourci « Envoyer » depuis `EmailTemplates/Index` vers cet écran.

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
- Attestations fiscales : **décidé par activité** (question ouverte n°4). Pas encore construit (indépendant des 4 tableaux ci-dessus) — reste dans le backlog.

---

## Ordre proposé

1. **Lot A** (navigation) — fondation dont dépendent tous les écrans suivants.
2. **Lot C** (présences) + **Lot B** (confirmation inscriptions) — cœur du quotidien coordinateur, forte valeur perçue.
3. **Lot D** (comptes) — gros morceau, à découper en sous-lots si besoin (D1 liste/tickets, D2 catégories/budget, D3 auto-calcul Équipe/PAF, D4 rapport).
4. **Lot G** (équipe) et **Lot F** (excursions) — périmètre plus contenu.
5. **Lot E** (e-mails) — débloqué (décision prise, question n°5) ; implique d'adapter le Lot 4 existant.
6. **Lot H** (ONE) — dépend d'une décision externe (attestations), peut être traité en parallèle sur les tableaux seuls.

Chaque lot doit démarrer par une session de clarification des questions ouvertes qui le concernent, avant tout code.
