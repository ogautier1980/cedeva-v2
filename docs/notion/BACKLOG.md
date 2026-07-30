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
6. **🔎 Précisé (2026-07-30, captures annotées) — Menu hamburger (barre latérale globale)** — Thomas veut le sortir des pages internes pour ne le garder qu'en page d'accueil. Deux captures avec annotations rouges/vertes clarifient une bonne partie de la cible :
   - `18.08.04` (page d'accueil actuelle, annotée) : croix rouges sur **toute la barre latérale**, les 4 cartes stats, le panneau « Inscriptions récentes » et les 5 boutons « Actions rapides » ; encadré vert sur **« Activités récentes »** (à garder seul). Confirme que la home doit devenir : liste des stages, un point c'est tout.
   - `18.38.25` (mockup dessiné par Thomas) : sous la liste des stages (« Stage 1/2/3/4 »), une section **« Paramètres généraux »** avec des boutons **Organisateur, Enfants, Parents, Modèles emails, Catégories Compte**. Ça montre concrètement où déplacer le contenu du hamburger : pas supprimé, redescendu en bas de la page d'accueil.
   - **Reste flou** : où vont Contacts, Importer des parents/enfants, Équipe (pas montrés dans le mockup — probablement dans la même section « Paramètres généraux » par cohérence, à confirmer avec Thomas) ; et Activités/Inscriptions (probablement remplacés par la liste de stages elle-même, qui fait déjà office d'écran "Activités").

---

## Lot A — Accueil & Navigation (fondation, prioritaire)

Tout le reste s'appuie sur cette navigation ; à faire en premier.

- ✅ **Fait (2026-07-29)** — Tableau de bord d'activité : logique inversée dans `Home/Index.cshtml` — clic sur le **titre** de l'activité → `ActivityManagement/Index` (les 7 gros boutons) ; clic sur le bouton (renommé **« Paramètres »**, resx `Home.Manage`, FR/EN/NL) → `/Activities/Details` (paramètres de l'activité).
- 🔎 **Précisé (2026-07-30, capture `18.08.04` annotée)** — Page d'accueil réduite à la seule liste des stages. La capture confirme exactement quoi retirer de `Home/Index.cshtml` : les 4 cartes stats (Activités actives, Inscriptions confirmées, Enfants/Parents, Membres d'équipe), le panneau « Inscriptions récentes », les 5 boutons « Actions rapides », toute la barre latérale — pour ne garder que le panneau « Activités récentes » (liste des stages). Voir aussi question ouverte n°6 (le hamburger renaît en dessous, section « Paramètres généraux »). — **pas encore codé**.
- Clic sur un stage → écran détail/tableau de bord de l'activité. — déjà le cas via le changement ci-dessus.
- ✅ **Fait (2026-07-29)** — Menu du haut (dans une activité) : `_Layout.cshtml` ne garde plus que **« Tableau de bord »** (retour aux 7 boutons) + un dropdown **« Pages spéciales »** (Liste des groupes, Total des présences). Les anciens raccourcis directs (Inscriptions, Présences, Comptes, E-mails, Excursions, Équipe, ONE) ont été retirés du haut — ils restent accessibles via les 7 boutons ; pour Comptes/E-mails dont les sous-pages (Transactions/TeamSalaries/Report, SentEmails/EmailTemplates) n'étaient QUE dans ce dropdown, des liens de remplacement ont été ajoutés en page (Financial/Index avait déjà ces liens ; ajoutés sur `SendEmail.cshtml`).
- ⏸️ **Non traité, mais largement précisé (question ouverte n°6)** — Menu hamburger (barre latérale globale) : les captures annotées montrent la cible (contenu redescendu en section « Paramètres généraux » sur la page d'accueil). Reste à confirmer avec Thomas où vont Contacts/Import/Équipe avant de coder.

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

- 🔎 **Précisé (2026-07-30, captures `18.48.58 1` et `18.50.34` annotées)** — Simplification plus radicale que prévu, en 2 temps :
  1. Le **« Tableau de bord financier »** (`Financial/Index`, atteint en cliquant sur la carte « Comptes ») doit quasiment disparaître : capture annotée avec croix rouges sur les 4 cartes stats (Revenus/Dépenses/Solde/Paiements en attente) **et** sur la carte « Salaires de l'équipe » — ne restent que **Transactions** et **Rapport**. Cohérent avec le texte de Thomas (« Quand on clique sur COMPTE, on arrive sur la liste des transactions ») : cliquer sur Comptes devrait sans doute sauter directement sur `Transactions`, ce tableau de bord intermédiaire n'a plus lieu d'être.
  2. La page **Transactions** elle-même doit perdre ses 3 cartes stats (Total revenus/dépenses/Solde net) et ses 3 onglets de filtre (Toutes/Revenus uniquement/Dépenses uniquement) — capture annotée avec croix rouges dessus. Ne reste que « Juste la liste » (texte de Thomas) + les boutons Ajouter paiement/dépense.
  - ✅ **Fait (2026-07-29)** — Bouton **« Masquer les montants »** ajouté sur `Financial/Transactions.cshtml` (flou CSS + `localStorage`) — reste valable, à priori compatible avec la simplification ci-dessus.
  - **Pas encore fait** : le reste de cette simplification (retirer les 2 pages intermédiaires, ou rediriger Comptes directement vers Transactions).
- Détail de ligne limité à la catégorie (rien de plus). — déjà globalement le cas (colonne « Détails » = catégorie/excursion/assigné), non retouché.
- 🔎 **Précisé (2026-07-30, capture `19.02.39`, référence de l'ancien système)** — Chaque ligne porte un **ID « numéro de ticket »** unique. La référence montre un entier simple et global (91, 126, 129… jusqu'à 198), pas de reset apparent par activité/catégorie — suggère un compteur séquentiel par organisation, jamais remis à zéro. Cette même capture révèle aussi un champ **« Caisse / Compte »** (le paiement/dépense est en caisse ou sur le compte bancaire) qui n'existe pas du tout aujourd'hui dans `Payment`/`Expense` — à voir si Thomas le veut aussi. — **pas encore codé**, migration nécessaire.
- Fusionner les écrans **« Ajouter un paiement »** et **« Ajouter une dépense »** en un seul écran (après clarification, question ouverte n°2). — non traité, toujours bloqué par la question ouverte.
- 🔎 **Précisé (2026-07-30, captures `18.58.21`/`19.00.10`, référence de l'ancien système)** — Catégories : la référence montre 3 colonnes **Entrées / Sorties / Hors bilan**, donc « Hors bilan » est un **3e état du même champ Type** (pas un flag séparé). Le formulaire référence a : Type (dropdown Entrée/Sortie/Hors bilan), Nom, Budget, et **« Lié à un enfant ? »** (Oui/Non) — ce dernier champ n'existe pas encore et explique probablement comment le système relie une catégorie comme "P.A.F." aux paiements/réservations pour l'auto-calcul.
  - ✅ **Fait (2026-07-29, partiel)** — `ExpenseCategory` a un champ **`IsIncome`** (bool Entrée/Sortie) et un **`Budget`** optionnel (migration `AddExpenseCategoryTypeAndBudget`). ⚠️ **À revoir** à la lumière de la référence : `IsIncome` devrait sans doute devenir un **enum à 3 valeurs** (Entrée/Sortie/HorsBilan) plutôt qu'un bool, et il manque le champ « Lié à un enfant ? ». Les `Payment` (Entrées/PAF) n'ont toujours aucune notion de catégorie — seuls les `Expense` (Sorties) en ont une.
- Bilan présenté en 2 grandes catégories : **Entrées** / **Sorties**, plus un statut **« Hors bilan »**. — **pas encore fait** ; voir ci-dessus, le concept se clarifie (3e valeur de Type) mais toucherait `FinancialCalculationService` à plusieurs endroits.
- ✅ **Déjà satisfait, vérifié (2026-07-29)** — Sous-catégories « Équipe » (Sorties) et « PAF » (Entrées) : **déjà 100% auto-calculées** aujourd'hui (`FinancialCalculationService.CalculateTeamMemberSalary` pour Équipe, agrégation des `Payment.Amount` pour PAF) — aucune saisie manuelle, rien à coder. Confirmé en lisant le code, pas juste supposé.
- Clic sur une sous-catégorie → liste des tickets liés à cette catégorie. — non retouché (dépend du numéro de ticket ci-dessus). Référence exacte de la liste attendue : capture `19.02.39` (Date/Nom/Entrées/Sorties/Caisse-Compte/Catégorie/Ticket).
- 🔎 **Précisé (2026-07-30, capture `19.00.54`, référence de l'ancien système)** — Format cible du Rapport, plus détaillé que ce qui a été fait : en-tête avec nom/adresse de l'organisation + n° d'entreprise, puis section **Entrées** et section **Dépenses** listant **chaque catégorie** avec 4 colonnes (Entrées/Sorties/Bilan/Budget), un total **« Bilan selon système »**, et une section **« Hors bilan »** séparée (Entrées/Sorties seulement, sans Bilan/Budget — cohérent avec « n'impacte pas le bilan »).
  - ✅ **Fait (2026-07-29)** — Rapport (`Financial/Report.cshtml`) restructuré en 2 colonnes **Entrées**/**Sorties**, même code couleur que Transactions. Pure présentation. ⚠️ **Ne va pas aussi loin que la référence** : pas de détail par catégorie avec Budget, pas de section Hors bilan — à revoir une fois les catégories Hors bilan/Budget en place.

## Lot E — E-mails

- 🔎 **Précisé (2026-07-30, capture `19.12.49 1` annotée)** — Épurer l'UI de la page d'envoi de mail (`SendEmail.cshtml`). L'annotation (comparée à la capture "avant" `19.12.49`) montre précisément quoi faire :
  - Retirer le panneau **« Informations »** (droite, bas) — « Options de destinataires » + « Conseils » — barré d'une croix rouge.
  - Retirer les boutons **« Enregistrer comme modèle »** et **« Historique des e-mails envoyés »** du bas du formulaire — barrés d'une croix rouge. Ne resteraient que **Envoyer** et **Annuler**.
  - Simplifier/dégrader en note discrète la case à cocher **« Un email par enfant »** — barrée d'une ligne rouge, avec le texte d'aide juste en dessous souligné en vert (à garder, sous une forme plus légère).
  - Déplacer/réduire le panneau **« Variables de personnalisation »** (droite, haut) — entouré d'un cercle vert avec une flèche vers le bas : suggère de le faire disparaître de la vue principale et de le rendre accessible autrement (lien discret, popup, ou accordéon replié) plutôt qu'un gros panneau permanent à droite.
  - **Pas encore codé.**
- ✅ **Fait (2026-07-30)** — **3 modèles verrouillés** : Confirmation d'inscription (`BookingConfirmation`), Rappel fiche médicale (`MedicalSheetReminder`), Rappel paiement (`PaymentReminder`) sont maintenant uniques au niveau organisation — modifiables mais **non créables, non supprimables, non duplicables** (`EmailTemplateTypeExtensions.IsLocked()`, garde-fous dans `EmailTemplateService`/`EmailTemplatesController`). Le Lot 4 (copie automatique par activité) a été adapté : ces 3 types ne sont plus jamais copiés/importés dans une activité ; migration `CleanupLockedEmailTemplateActivityCopies` supprime les copies déjà existantes en base (décision : on perd les personnalisations par activité déjà faites, cf. question n°5). Au passage, le libellé `BookingConfirmation` corrigé de « Confirmation de réservation » → « Confirmation d'inscription » (4 langues) pour matcher le vocabulaire métier.
- Modèles **Excursion** : libres — création/modification/suppression sans restriction (déjà le cas, inchangé par ce qui précède).
- ✅ **Répondu (2026-07-30, question n°3)** — le parcours d'envoi depuis un modèle existe déjà (menu déroulant + auto-remplissage dans `SendEmail.cshtml`). ✅ **Fait (2026-07-30)** — ajout du raccourci manquant : bouton « Envoyer » sur `EmailTemplates/Index` qui ouvre `SendEmail` avec le modèle déjà chargé (nouveau paramètre `templateId`), visible uniquement en contexte d'activité (la bibliothèque org n'a pas d'activité cible évidente).

## Lot F — Excursions

- 🔎 **Précisé (2026-07-30, capture `19.22.35` annotée)** — Le formulaire « Créer une excursion » est annoté précisément : croix rouge/verte sur **Heure de début**, **Heure de fin** et **Type** (le dropdown "Piscine") uniquement. **Nom, Description, Date, Coût et Groupes cibles ne sont PAS barrés** — donc à garder. Ça corrige/affine le texte brut de Thomas ("Titre, date et groupe") : la cible réelle est Nom+Description+Date+Coût+Groupes, sans Heure début/fin ni Type. — non traité (rien retiré du formulaire actuel).
- 🔎 **Précisé (2026-07-30, capture `19.25.31` annotée)** — La liste « Gérer les excursions » est aussi annotée : croix rouge sur la colonne **Type d'excursion** et sur les colonnes financières (**Coût par enfant/Revenus totaux/Dépenses totales/Solde net**) d'une ligne — à simplifier en gardant priorité à Nom/Date/Groupes ciblés/Inscriptions/Actions. — non traité.
- Ne pas supprimer les champs actuels du modèle — les conserver en base pour une réintroduction ultérieure (Thomas prévoit de les redemander).
- 🐛 **Bug trouvé et corrigé (2026-07-29)** — `Excursions.SendEmail` (POST) ne faisait qu'afficher « X emails envoyés » sans jamais réellement envoyer quoi que ce soit (aucun appel à un service d'email). Corrigé : envoie vraiment maintenant (variables fusionnées par enfant ou brut par parent, pièce jointe incluse), comme le fait déjà `ActivityManagement.SendEmail`.
- Quand une excursion est programmée : proposer automatiquement l'envoi d'un mail « Excursion ». — **pas fait, et plus compliqué que prévu** : les options de destinataires actuelles (« tous les inscrits », « groupe X inscrits ») ne fonctionnent QUE pour des enfants déjà inscrits à l'excursion. Juste après la création, personne n'est encore inscrit → rediriger automatiquement vers l'écran d'envoi tomberait sur « Aucun destinataire trouvé ». Ce que Thomas décrit (annoncer la nouvelle excursion aux familles éligibles, même non encore inscrites) est un destinataire différent, pas encore supporté — à cadrer avant de coder l'auto-proposition.

## Lot G — Équipe

- 🔎 **Précisé (2026-07-30, capture `19.34.30` annotée)** — Alléger la vue principale : la capture montre une croix rouge sur tout le panneau **« Membres disponibles »** (colonne de droite, liste des membres non assignés + bouton Ajouter) — à retirer de cette vue (déplacé « en dessous », probablement en historique/section secondaire, cf. texte de Thomas). Le panneau « Équipe assignée » (gauche) n'est pas touché. — non traité.
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
5. **Lot E** (e-mails) — cœur fait (3 modèles verrouillés + raccourci d'envoi) ; reste « épurer l'UI » de `SendEmail.cshtml`, maintenant précisé par capture annotée (retirer panneau Informations + boutons modèle/historique, alléger Variables de personnalisation).
6. **Lot H** (ONE) — dépend d'une décision externe (attestations), peut être traité en parallèle sur les tableaux seuls.

Chaque lot doit démarrer par une session de clarification des questions ouvertes qui le concernent, avant tout code.
