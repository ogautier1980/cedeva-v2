# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier (certaines annotées par Thomas en rouge/vert) + exemple [`17.pdf`](17.pdf) (listing ONE).

**Mise à jour 2026-07-30** — 5 des 10 questions posées après la relecture complète ont été tranchées par le user : attestations fiscales (par association), présences équipe (confirmé), richesse de la liste des groupes (confirmé, avec une précision), impression groupée des groupes (confirmé), ventilation ONE des présences (confirmé). Il ne reste que 5 questions réellement ouvertes.

---

## ⚠️ Questions ouvertes pour Thomas

1. **Paiements partiels / CPAS / facture** — le coordinateur doit-il pouvoir forcer une inscription non payée et encoder un paiement manuel ? Quelles règles (échéancier, tiers payant) ?
2. **Menu hamburger** — la cible est connue (contenu redescendu en section « Paramètres généraux » sur la page d'accueil, capture `18.38.25`), mais où vont précisément Contacts, Importer des parents/enfants et Équipe (pas montrés dans le mockup) ?
3. **Numéro de ticket** (Lot D) — quel schéma de numérotation ? (par activité, par organisation, remise à zéro annuelle ou jamais). La référence (capture `19.02.39`) suggère un compteur global jamais remis à zéro.
4. **« Hors bilan »** (Lot D) — au-delà du principe (3e valeur du champ Type, confirmé par la référence), quelles transactions concrètes doivent y aller précisément ?
5. **Auto-proposition mail Excursion** (Lot F) — Thomas veut annoncer une nouvelle excursion aux familles éligibles **avant même qu'elles soient inscrites** ; ça n'existe pas dans le système de destinataires actuel (qui ne cible que des enfants déjà inscrits). Un nouveau type de destinataire est à concevoir avec Thomas.

*(Résolues et retirées de cette liste — détail dans le lot concerné : explication paiement/dépense → Lot D ; envoi depuis un modèle et conflit Lot 4 → Lot E ; attestations fiscales, présences équipe, richesse des groupes, impression groupée, ventilation ONE → voir Lots C/G/H ci-dessous.)*

## 📌 TO-DO (hors backlog UX)

- **Passer à OVH (VPS-2)** — migration d'hébergement (actuellement Azure). Implique une **migration vers PostgreSQL** (actuellement SQL Server).
- **Passer à Molly** — remplace Stripe comme solution de paiement en ligne (actuellement `StripePaymentGateway`, voir [ADR 0010](../adr/0010-online-payments-provider-agnostic-stripe.md)).

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
- ✅ **Fait, mais à enrichir (confirmé par Thomas)** — Pages spéciales :
  - **Liste des groupes imprimable** (`Groups`/`PrintGroups`) : Thomas confirme vouloir le niveau de richesse de la référence — ⚠️ précision reçue : les libellés « 3-4, 5-6, 7-8… » sur la capture `18.43.17` ne sont **pas une notion de tranche d'âge** à ajouter au modèle, ce sont simplement les **groupes créés pour cette activité-là** dans l'ancien système (comme nos « Groupe Rouge/Bleu/Vert » aujourd'hui) — rien à changer côté structure des groupes. Ce qui reste à ajouter : **sélection multiple** de groupes (au lieu d'un seul à la fois), des **options à cocher** Prévus/Présent/Signature pour choisir les colonnes affichées, une **colonne Signature** vide (émargement papier), et un **export PDF et un export Excel** (en plus de l'impression navigateur actuelle).
  - **Confirmé — « Imprimer tous les groupes du jour »** en un clic (action groupée distincte de l'impression groupe par groupe actuelle).
  - **Confirmé — Total des présences journalières** (`PresenceSummary`) : à décomposer par indicateur ONE (Milieu défavorisé / Handicap léger / Handicap lourd) en plus du total brut réservé/présent (réf. capture `18.48.14`).

## Lot D — Comptes / Finances

- ⏸️ **Pas fait** — Simplification du parcours Comptes → Transactions : la cible (captures `18.48.58 1`/`18.50.34`) est de sauter directement sur la liste des transactions nue (sans les cartes stats ni les onglets de filtre) en cliquant sur « Comptes ».
- ✅ **Fait** — Bouton « Masquer les montants » sur `Transactions.cshtml`.
- ⏸️ **Pas fait** — Numéro de ticket unique par ligne (question n°3) ; révèle aussi un champ **« Caisse / Compte »** (cash vs bancaire) absent du modèle actuel.
- **Fusion Ajouter un paiement / Ajouter une dépense** — Thomas l'a demandé explicitement (*« pour moi qu'on ajoute un paiement ou une dépense doit être le même écran »*), donc ce n'est plus une question ouverte. Analyse technique faite : `Payment` (clé = réservation) et `Expense` (clé = activité) n'ont ni la même clé ni les mêmes champs — fusionner est possible via un écran à bascule Entrée/Sortie mais héberge deux formulaires distincts, pas un vrai formulaire unique. **Pas encore construit.**
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
- ⏸️ **Confirmé par Thomas, pas encore fait** — Présences équipe jour/jour (miroir du système enfants). Reste un gros morceau technique : nouvelle entité + migration + UI, **et** ça change le calcul salarial actuel qui suppose aujourd'hui une présence à 100% de tous les jours de l'activité — le calcul devra utiliser les présences réelles une fois cochées.
- ✅ Déjà satisfait, vérifié : compléments/dépenses par membre (`Expense.TeamMemberId`), décompte total par personne (`TeamSalaries.cshtml`).
- ✅ **Fait** — Stockage de l'extrait de casier judiciaire (`TeamMember.CriminalRecordUrl`).

## Lot H — ONE (organisme officiel)

- ✅ **Fait** — 4 tableaux par activité (`ActivityManagement/OneReport`) : listings 2-5 ans / 6 ans et plus (N°, nom, âge, dates, jours, prix payé, indicateurs) + présences hebdomadaires par tranche d'âge. Format calqué sur [`17.pdf`](17.pdf). Aucune migration nécessaire, testé (`OneReportTests.cs`). **Ce rapport reste par activité** (c'est un rapport officiel envoyé à l'ONE, distinct de l'attestation fiscale ci-dessous — à ne pas confondre).
- ⏸️ **Confirmé par Thomas, pas encore fait** — Attestations fiscales : **regroupées par association**, pas par activité (confirmation du texte original — l'attestation fiscale donnée au parent est un document différent du rapport ONE par activité ci-dessus). Aucune attestation fiscale n'existe encore dans le code — à construire.

---

## Ordre proposé

1. **Lot A** — reste : cadrer le hamburger (question n°2).
2. **Lot C** — enrichir les pages spéciales (groupes multi-sélection + export PDF/Excel + signature, impression groupée, ventilation ONE des présences) : tout est confirmé, codable directement.
3. **Lot D** — le plus gros morceau restant : simplification Comptes→Transactions (codable directement), puis fusion paiement/dépense, numéro de ticket (n°3), Hors bilan (n°4), Rapport détaillé.
4. **Lot G** — présences équipe : confirmé mais gros morceau (impact calcul salarial), codable mais à planifier avec soin.
5. **Lot F** — auto-proposition mail Excursion (question n°5).
6. **Lot E** — reste : épurer l'UI de SendEmail (codable directement).
7. **Lot H** — attestations fiscales par association : confirmé, à construire (aucune base existante).

La majorité du backlog restant est maintenant confirmée et codable sans attendre Thomas ; seules les questions 1 à 5 ci-dessus bloquent encore quelque chose.
