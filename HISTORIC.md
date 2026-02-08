# Cedeva - Historique des Décisions de Développement

Ce fichier résume les documents historiques de planification et d'analyse créés pendant le développement de Cedeva v2.

---

## Migration v1 → v2 (Analyse du 2026-01-24)

### Architecture
- **v1**: Cedeva.Web + Cedeva.Infrastructure, Repository Pattern, Navbar
- **v2**: Clean Architecture (Website + Core + Infrastructure), Feature Folders, Sidebar

### Décisions clés
- **Garder**: Feature folders, CedevaUserClaimsPrincipalFactory, accès direct DbContext
- **Abandonner**: Repository Pattern (trop verbeux), ClaimsTransformer, ancienne structure
- **Récupérer du v1**: Palette couleurs (#007faf/#005778), dashboard ActivityManagement, gestion emails, autocomplete adresses belges

### Fonctionnalités récupérées depuis v1 (toutes implémentées)
- ActivityManagement avec dashboard cards, UnconfirmedBookings, Presences, SendEmail
- Gestion des utilisateurs (CedevaUsers → Users)
- Pagination composant réutilisable
- Autocomplete villes belges
- Design: couleurs, cards avec ombres, FontAwesome icons

---

## Module Financier (Plan du 2026-01-31 — IMPLÉMENTÉ)

### Fonctionnalités livrées
- **Communications structurées belges** (format +++XXX/XXXX/XXXXX+++ avec modulo 97)
- **Import fichiers CODA** (relevés bancaires belges, format fixe 128 chars/ligne)
- **Rapprochement bancaire** auto (par communication structurée) + manuel
- **Paiements manuels** (cash)
- **Calcul salaires équipe** (affichage uniquement — pas de transaction enregistrée)
- **Notes de frais et consommations personnelles** animateurs
- **Rapport financier** par activité avec Chart.js
- **Export Excel/PDF**

### Décisions de conception
- Salaires = calcul dynamique seulement (jours présents × défraiement + remboursements - consommations perso), jamais enregistrés en transaction
- Tous coordinateurs ont accès à la gestion financière (pas de nouveau rôle)
- Architecture extensible: `TransactionCategory` enum pour futures excursions
- Chart.js pour visualisations (camembert revenus/dépenses, barres par catégorie)

### Entités créées
- `Payment`, `BankTransaction`, `CodaFile`, `ActivityFinancialTransaction`
- Enums: `PaymentMethod`, `PaymentStatus`, `TransactionType`, `TransactionCategory`, `ExpenseType`
- Colonnes ajoutées à `Booking` (StructuredCommunication, TotalAmount, PaidAmount, PaymentStatus)
- Colonnes ajoutées à `Organisation` (BankAccountNumber, BankAccountName)

---

## Localisation (Travail du 2026-01-31)

### État des traductions
- **Français (FR)**: Complet (langue principale, ~1024 clés)
- **Anglais (EN)**: 290 clés traduites, ~734 restantes avec préfixe `[EN]`
- **Néerlandais (NL)**: 290 clés traduites, ~734 restantes avec préfixe `[NL]`

### Catégories déjà traduites (EN/NL)
- Boutons (Button.*), jours de la semaine (DayOfWeek.*), termes communs
- En-têtes colonnes Excel, méthodes/statuts de paiement, types de dépenses
- Labels de champs simples, termes de recherche

### Ce qui reste à traduire (~734 clés)
- Phrases complexes (Account.*, Messages, Erreurs, Validation, Email content)
- Nécessite traduction professionnelle ou avec contexte métier

---

## Gestion des Questions/Réponses dans Bookings (Plan 2026-02)

> Note: Ce plan (vivid-tumbling-deer) est toujours actif dans le système. Il concerne
> l'affichage/édition des réponses aux questions d'activité dans les vues Booking
> (Details/Create/Edit). La gestion des questions elle-même (drag & drop, toggle IsActive)
> est déjà implémentée via ActivityQuestionsController (commit c668830).
