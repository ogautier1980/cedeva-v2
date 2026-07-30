# Backlog CEDEVA 2.0 — Retour UX/Fonctionnel (Notion, 2026-07-28)

Source : export Notion [`CEDEVA 2 0 ....md`](CEDEVA%202%200%2035545c93462a801cae9dd5fc2c518849.md) + captures d'écran du même dossier (certaines annotées par Thomas en rouge/vert) + exemple [`17.pdf`](17.pdf) (listing ONE).

**Mise à jour 2026-07-30** — document réécrit pour rester lisible : les questions entièrement résolues et déjà codées ont été retirées de la liste des questions ouvertes (le détail reste dans le lot concerné, en une ligne). Toutes les captures ont été relues (y compris les 5 non examinées lors des passes précédentes), ce qui a fait remonter 3 questions supplémentaires (n° 8-10 ci-dessous) et un écart texte/décision à trancher (n°2).

---

## ⚠️ Questions ouvertes pour Thomas

1. **Paiements partiels / CPAS / facture** — le coordinateur doit-il pouvoir forcer une inscription non payée et encoder un paiement manuel ? Quelles règles (échéancier, tiers payant) ?
2. **Attestations fiscales ONE — par activité ou par association ?** Le texte original dit littéralement : *« Can, idéalement, ce n'est pas par activité mais regroupé par association. On en discute. »* — c'est-à-dire l'inverse de ce qui avait été noté ici après une consigne reçue en session (« par activité »). Vu la contradiction, **à retrancher explicitement avec Thomas** avant de construire quoi que ce soit (rien n'est codé à ce stade). Voir Lot H.
3. **Menu hamburger** — la cible est connue (contenu redescendu en section « Paramètres généraux » sur la page d'accueil, capture `18.38.25`), mais où vont précisément Contacts, Importer des parents/enfants et Équipe (pas montrés dans le mockup) ?
4. **Numéro de ticket** (Lot D) — quel schéma de numérotation ? (par activité, par organisation, remise à zéro annuelle ou jamais). La référence (capture `19.02.39`) suggère un compteur global jamais remis à zéro.
5. **« Hors bilan »** (Lot D) — au-delà du principe (3e valeur du champ Type, confirmé par la référence), quelles transactions concrètes doivent y aller précisément ?
6. **Auto-proposition mail Excursion** (Lot F) — Thomas veut annoncer une nouvelle excursion aux familles éligibles **avant même qu'elles soient inscrites** ; ça n'existe pas dans le système de destinataires actuel (qui ne cible que des enfants déjà inscrits). Un nouveau type de destinataire est à concevoir avec Thomas.
7. **Présences équipe jour/jour** (Lot G) — gros morceau : en plus d'une nouvelle entité/UI, ça changerait le calcul salarial actuel (qui suppose aujourd'hui une présence à 100%) → impact financier réel, à valider avant de coder.
8. **🆕 Liste des groupes imprimable — richesse attendue ?** Les captures de référence (`18.43.17`, `18.47.34`, non examinées lors des passes précédentes) montrent un système nettement plus riche que ce qui a été construit : groupes **par tranche d'âge** (3-4, 5-6, 7-8, 9-10, 11+) avec **sélection multiple**, options **Prévus/Présent/Signature** à la carte, **export PDF et Excel** (pas juste une impression navigateur), et une colonne **Signature** pour émargement papier. Ce qui existe (`ActivityManagement/Groups` + `PrintGroups`) utilise les groupes nommés de Cedeva (Rouge/Bleu/Vert), une sélection simple, et impression seule. Thomas veut-il ce niveau de richesse, ou la version actuelle suffit-elle ?
9. **🆕 « Imprimer tous les groupes du jour » en un clic** — action distincte visible dans la référence (capture `18.43.17`), pas construite (aujourd'hui on imprime groupe par groupe).
10. **🆕 « Total des présences » devrait ventiler par indicateur ONE** — la référence (capture `18.48.14`) montre le total journalier décomposé en Milieu défavorisé / Handicap léger / Handicap lourd (en plus du brut), alors que `PresenceSummary` (déjà construit) ne calcule que le brut réservé/présent. À enrichir si Thomas le confirme.

*(Résolues et déjà codées, donc retirées de cette liste : l'explication des écrans paiement/dépense — Thomas a une préférence connue, voir Lot D ; l'existence du parcours d'envoi depuis un modèle — Lot E ; le conflit Lot 4/Lot E sur les modèles verrouillés — tranché et codé, Lot E.)*

---

## Lot A — Accueil & Navigation

- ✅ **Fait** — Tableau de bord d'activité : clic sur le **titre** → 7 gros boutons (`ActivityManagement/Index`) ; clic sur **« Paramètres »** (renommé, ex-« Gérer ») → réglages de l'activité (`Activities/Details`).
- ✅ **Fait** — Page d'accueil réduite à la liste « Activités récentes » (4 cartes stats, inscriptions récentes, actions rapides retirées, `HomeController` simplifié en conséquence).
- ✅ **Fait** — Menu du haut (dans une activité) réduit à « Tableau de bord » + dropdown « Pages spéciales » (liste des groupes, total des présences).
- ⏸️ **Non traité** — Menu hamburger (barre latérale globale) : cible connue mais reste à cadrer, voir question n°3.

## Lot B — Confirmation des inscriptions

- ✅ Déjà le cas : ne montre que les inscriptions « à confirmer ».
- ✅ **Fait** — Clic sur le nom de l'enfant → fiche `Bookings/Details` (voir Lot C).
- ⏸️ **Pas fait, volontairement mis de côté** — Le coordinateur encode le montant à payer → mail au parent avec lien de paiement. Sensible (touche Stripe/facturation), à reprendre dans une session dédiée.

## Lot C — Présences

- ✅ **Fait** — Clic sur un enfant → fiche `Bookings/Details` enrichie (adresse/parent éditables, groupe, fiche médicale, historique des paiements).
- ✅ Confirmé dans le code : le filtre jour est déjà scopé à l'activité + jour sélectionné.
- ✅ **Fait** — Colonne « Payé » (✓/✗ + solde) dans `Presences.cshtml`.
- ⏸️ La partie « forcer une inscription non payée » dépend de la question n°1.
- ✅ **Fait** — Pages spéciales : liste des groupes imprimable (`Groups`/`PrintGroups`) et total des présences journalières (`PresenceSummary`). ⚠️ Voir questions n°8-10 : la référence retrouvée après-coup suggère une version plus riche pour ces deux pages.

## Lot D — Comptes / Finances

- ⏸️ **Pas fait** — Simplification du parcours Comptes → Transactions : la cible (captures `18.48.58 1`/`18.50.34`) est de sauter directement sur la liste des transactions nue (sans les cartes stats ni les onglets de filtre) en cliquant sur « Comptes ».
- ✅ **Fait** — Bouton « Masquer les montants » sur `Transactions.cshtml`.
- ⏸️ **Pas fait** — Numéro de ticket unique par ligne (question n°4) ; révèle aussi un champ **« Caisse / Compte »** (cash vs bancaire) absent du modèle actuel.
- **Fusion Ajouter un paiement / Ajouter une dépense** — Thomas l'a demandé explicitement (*« pour moi qu'on ajoute un paiement ou une dépense doit être le même écran »*), donc ce n'est plus une question ouverte. Analyse technique faite : `Payment` (clé = réservation) et `Expense` (clé = activité) n'ont ni la même clé ni les mêmes champs — fusionner est possible via un écran à bascule Entrée/Sortie mais héberge deux formulaires distincts, pas un vrai formulaire unique. **Pas encore construit.**
- ⏸️ **Partiel** — Catégories : `ExpenseCategory.IsIncome` (bool) + `Budget` ajoutés. La référence (`18.58.21`/`19.00.10`) montre en réalité 3 valeurs (Entrée/Sortie/**Hors bilan**, question n°5) et un champ **« Lié à un enfant ? »** absent — à revoir.
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
- ⏸️ **Pas fait** — Auto-proposition d'un mail « Excursion » à la programmation : bloqué par la question n°6 (nouveau type de destinataire à concevoir).

## Lot G — Équipe

- ✅ **Fait** — Panneau « Membres disponibles » déplacé sous « Équipe assignée », replié par défaut.
- ⏸️ **Pas fait** — Présences équipe jour/jour : question n°7 (impact salarial à valider avant de coder).
- ✅ Déjà satisfait, vérifié : compléments/dépenses par membre (`Expense.TeamMemberId`), décompte total par personne (`TeamSalaries.cshtml`).
- ✅ **Fait** — Stockage de l'extrait de casier judiciaire (`TeamMember.CriminalRecordUrl`).

## Lot H — ONE (organisme officiel)

- ✅ **Fait** — 4 tableaux par activité (`ActivityManagement/OneReport`) : listings 2-5 ans / 6 ans et plus (N°, nom, âge, dates, jours, prix payé, indicateurs) + présences hebdomadaires par tranche d'âge. Format calqué sur [`17.pdf`](17.pdf). Aucune migration nécessaire, testé (`OneReportTests.cs`).
- ⏸️ **Pas fait, en question** — Attestations fiscales : voir question n°2 (par activité ou par association — écart texte/décision à trancher).

---

## Ordre proposé

1. **Lot A** — reste : cadrer le hamburger (question n°3).
2. **Lot C** — questions n°8-10 sur la richesse des pages spéciales, à trancher avec Thomas avant d'investir plus.
3. **Lot D** — le plus gros morceau restant : simplification Comptes→Transactions (pas de question bloquante, codable directement), puis fusion paiement/dépense, numéro de ticket (n°4), Hors bilan (n°5), Rapport détaillé.
4. **Lot G** — présences équipe (question n°7, gros morceau) ; le reste est fait.
5. **Lot F** — auto-proposition mail Excursion (question n°6).
6. **Lot E** — reste : épurer l'UI de SendEmail (pas de question bloquante, codable directement).
7. **Lot H** — attestations fiscales, bloqué par la question n°2.

Les items marqués « pas de question bloquante, codable directement » peuvent être attaqués sans attendre Thomas.
