# Cedeva

**Cedeva** est une application web ASP.NET Core MVC pour la gestion de centres d'activités et de stages de vacances pour enfants en Belgique. SaaS multi-tenant où les organisations gérent les activités, les inscriptions des enfants, les équipes d'animation, les communications avec les parents et le suivi financier.

---

## Démarrage rapide

```bash
docker-compose up -d                                          # Démarrer SQL Server
dotnet run --project src/Cedeva.Website                       # Lancer (seeder automatique)
```

| Compte | Email | Mot de passe |
|--------|-------|--------------|
| Admin | admin@cedeva.be | Admin@123456 |
| Coordinateur (Org 1) | coordinator@cedeva.be | Coord@123456 |
| Coordinateur (Org 2) | coordinator.liege@cedeva.be | Coord@123456 |

---

## Pile technologique

| Composant | Technologie |
|-----------|-------------|
| Backend | ASP.NET Core MVC (.NET 10) |
| Base de données | SQL Server 2022 (Docker) |
| ORM | Entity Framework Core 10 |
| Paiement en ligne | Stripe Checkout via `IPaymentGateway` (agnostique du fournisseur) |
| Email | Brevo SDK (C#) |
| Excel | ClosedXML |
| Stockage de fichiers | Azure Blob Storage |
| Conteneur DI | Autofac |
| Logging | Serilog |
| Authentification | ASP.NET Core Identity |
| Localisation | FR / NL / EN (fichiers de ressources) |
| Frontend | Bootstrap 5 + jQuery + FontAwesome |

---

## Architecture

### Organisation du projet

```
src/
├── Cedeva.Website/            # Couche présentation MVC
│   ├── Features/              # Structure par dossier de fonctionnalité
│   │   ├── Activities/
│   │   ├── ActivityManagement/
│   │   ├── Bookings/
│   │   ├── Children/
│   │   ├── Parents/
│   │   ├── TeamMembers/
│   │   ├── Financial/
│   │   ├── Payments/
│   │   ├── EmailTemplates/
│   │   ├── PublicRegistration/
│   │   ├── Presence/
│   │   ├── Account/
│   │   ├── Organisations/
│   │   ├── Users/
│   │   └── Shared/            # Partials réutilisables (_AlertMessages, _Pagination, etc.)
│   ├── Localization/          # SharedResources.{fr,nl,en}.resx
│   └── wwwroot/               # Ressources statiques (css/cedeva.css)
├── Cedeva.Core/               # Couche domaine
│   ├── Entities/              # Entités EF Core
│   ├── Interfaces/            # Interfaces de services et de dépôts
│   └── Enums/                 # Toutes les énumérations
└── Cedeva.Infrastructure/     # Couche infrastructure
    ├── Data/                  # DbContext, migrations, seeder
    ├── Services/              # Email, Excel, PDF, stockage, paiements Stripe
    └── Repositories/          # Dépôt générique + unit of work
```

### Patterns clés

- **Multi-tenancy** — Filtrage par `OrganisationId` via les filtres de requête globaux d'EF Core. L'admin contourne les filtres. Le seeder utilise `.IgnoreQueryFilters()`.
- **Dossiers par fonctionnalité** — Chaque fonctionnalité est auto-contenue : contrôleur + vues + ViewModels dans un même dossier.
- **Dépôt + Unit of Work** — `IRepository<T>` / `IUnitOfWork`. Méthodes : `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
- **Localisation** — Sélection de la langue par cookie. Les vues injectent `IStringLocalizer<SharedResources>`. Convention des clés : `Field.X`, `Button.X`, `Message.X`, `Enum.X.Value`.
- **Alertes TempData** — Clés standardisées : `SuccessMessage`, `ErrorMessage`, `WarningMessage`. Rendues via le partial `_AlertMessages.cshtml`.

---

## Énumérations

| Énumération | Valeurs |
|-------------|---------|
| **Country** | Belgium (0), France (1), Netherlands (2), Luxembourg (3) |
| **Role** | Admin (0), Coordinator (1) |
| **TeamRole** | Animator (0), Coordinator (1) |
| **License** | License (0), Assimilated (1), Internship (2), Training (3), NoLicense (4) |
| **Status** | Compensated (0), Volunteer (1) |
| **QuestionType** | Text (0), Checkbox (1), Radio (2), Dropdown (3) |
| **EmailRecipient** | AllParents (0), ActivityGroup (1), MedicalSheetReminder (2) |
| **PaymentMethod** | BankTransfer (0), Cash (1), Other (2), Online (3) |
| **PaymentStatus** | NotPaid (0), PartiallyPaid (1), Paid (2), Overpaid (3), Cancelled (4) |
| **ExpenseType** | Reimbursement (0), PersonalConsumption (1) |
| **TransactionType** | Income (0), Expense (1) |
| **TransactionCategory** | Payment (0), TeamExpense (1), PersonalConsumption (2), Other (3) |
| **EmailTemplateType** | BookingConfirmation (1), WelcomeEmail (2), MedicalSheetReminder (3), PaymentReminder (4), ActivityCancellation (5), Custom (99) |

---

## Entités

### Organisation
Racine du multi-tenant. Chaque entité est liée à une organisation.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| Name | string (100) | Obligatoire |
| Description | string (500) | Obligatoire |
| AddressId | int | CE → Address |
| LogoUrl | string? | URL Azure Blob |
| BankAccountNumber | string? | IBAN pour le suivi des paiements |
| BankAccountName | string? | Nom du titulaire du compte |

### Activity
Stage / programme de vacances géré par une organisation.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| Name | string (100) | Obligatoire |
| Description | string (500) | Obligatoire |
| IsActive | bool | |
| PricePerDay | decimal? | Utilisé pour calculer les totaux de réservation |
| StartDate / EndDate | DateTime | Date uniquement |
| OrganisationId | int | CE → Organisation |
| Days | List\<ActivityDay> | Jours générés automatiquement |
| Groups | ICollection\<ActivityGroup> | |
| TeamMembers | ICollection\<TeamMember> | Relation many-to-many |
| Bookings | ICollection\<Booking> | |

### ActivityDay
Représente un jour calendaire dans une activité.

| Champ | Type | Notes |
|-------|------|-------|
| DayId | int | CP |
| Label | string (100) | ex. "Lundi 18/03" |
| DayDate | DateTime | |
| Week | int? | Numéro de semaine dans l'activité |
| IsActive | bool | |
| ActivityId | int | CE → Activity |

### ActivityGroup
Groupe nommé au sein d'une activité (ex. regroupement par âge).

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| Label | string (100) | |
| Capacity | int? | Nombre max d'enfants |
| ActivityId | int? | CE → Activity |

### ActivityQuestion / ActivityQuestionAnswer
Questions personnalisées par activité. Les réponses sont stockées par réservation.

| ActivityQuestion | Type | Notes |
|-----------------|------|-------|
| Id | int | CP |
| ActivityId | int | CE → Activity |
| QuestionText | string (500) | |
| QuestionType | QuestionType | Text / Checkbox / Radio / Dropdown |
| IsRequired | bool | |
| Options | string? | Séparé par des virgules pour Radio/Dropdown |

| ActivityQuestionAnswer | Type | Notes |
|------------------------|------|-------|
| Id | int | CP |
| BookingId | int | CE → Booking |
| ActivityQuestionId | int | CE → ActivityQuestion |
| AnswerText | string (1000) | |

### Address
Entité d'adresse partagée (Organisation, Parent, TeamMember).

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| Street | string (2–100) | |
| City | string (2–100) | |
| PostalCode | string (10) | Était int, changé en string pour les codes postaux internationaux |
| Country | Country | Par défaut : Belgium |

### Parent
Tuteur / personne de contact. Un parent peut avoir plusieurs enfants.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| FirstName / LastName | string (2–100) | |
| Email | string (100) | Validation EmailAddress |
| PhoneNumber | string? (100) | Regex téléphone fixe belge |
| MobilePhoneNumber | string (100) | Regex mobile belge |
| NationalRegisterNumber | string (11–15) | Numéro de registre national belge |
| AddressId | int | CE → Address |
| OrganisationId | int | CE → Organisation |
| FullName | calculé | "LastName, FirstName" |

### Child
Enfant inscrit. Lié à un Parent et peut avoir plusieurs Bookings.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| FirstName / LastName | string (2–100) | |
| **BirthDate** | DateTime | ⚠️ Le nom de la propriété est `BirthDate` (pas DateOfBirth) |
| NationalRegisterNumber | string (11–15) | |
| IsDisadvantagedEnvironment | bool | Milieu défavorisé |
| IsMildDisability | bool | Handicap léger |
| IsSevereDisability | bool | Handicap lourd |
| ParentId | int | CE → Parent |
| FullName | calculé | "LastName, FirstName" |

### TeamMember
Membre de l'équipe (animateur ou coordinateur). Utilise `TeamMemberId` comme clé primaire.

| Champ | Type | Notes |
|-------|------|-------|
| **TeamMemberId** | int | CP (pas `Id`) |
| FirstName / LastName | string (100) | |
| Email | string (100) | |
| BirthDate | DateTime | |
| MobilePhoneNumber | string (100) | |
| NationalRegisterNumber | string (11–15) | |
| TeamRole | TeamRole | Animator / Coordinator |
| License | License | Type de brevet |
| Status | Status | Compensated / Volunteer |
| DailyCompensation | decimal? | Indemnité journalière |
| LicenseUrl | string (100) | URL Azure Blob |
| AddressId | int | CE → Address |
| OrganisationId | int | CE → Organisation |
| Activities | ICollection\<Activity> | Relation many-to-many |

### Booking
Réservation d'un enfant pour une activité. Entité financière centrale.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| BookingDate | DateTime | |
| ChildId | int | CE → Child |
| ActivityId | int | CE → Activity |
| GroupId | int? | CE → ActivityGroup |
| **IsConfirmed** | bool | ⚠️ Drapeau booléen (pas une énumération de statut) |
| IsMedicalSheet | bool | Fiche médicale reçue |
| StructuredCommunication | string? | Format belge `+++XXX/XXXX/XXXXX+++` (mod-97) |
| TotalAmount | decimal | PricePerDay × jours réservés |
| PaidAmount | decimal | Somme des paiements |
| PaymentStatus | PaymentStatus | NotPaid / PartiallyPaid / Paid / Overpaid |
| Days | ICollection\<BookingDay> | |
| Payments | ICollection\<Payment> | |
| QuestionAnswers | ICollection\<ActivityQuestionAnswer> | |

### BookingDay
Relie une réservation à un jour d'activité spécifique.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| ActivityDayId | int | CE → ActivityDay |
| IsReserved | bool | Jour réservé |
| IsPresent | bool | Enfant présent |
| BookingId | int | CE → Booking |

### Payment
Enregistrement de paiement individuel pour une réservation.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| BookingId | int | CE → Booking |
| Amount | decimal | |
| PaymentDate | DateTime | |
| PaymentMethod | PaymentMethod | BankTransfer / Cash / Other |
| Status | PaymentStatus | |
| StructuredCommunication | string? | Communication structurée belge (référence de paiement) |
| Reference | string? | Référence fournisseur/libre (ex. id de session Stripe pour les paiements en ligne) |
| CreatedByUserId | int? | Utilisateur ayant enregistré le paiement manuel |

> **Note :** l'import CODA et le rapprochement bancaire (`CodaFile`, `BankTransaction`) ont été
> **supprimés** et remplacés par le paiement en ligne (Stripe) — voir [docs/adr/0010](docs/adr/0010-online-payments-provider-agnostic-stripe.md).

### Expense
Dépense financière liée à une activité. Deux catégories : dépenses de l'animateur et dépenses de l'organisation.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| Label | string (100) | |
| Description | string? (500) | |
| Amount | decimal | |
| Category | string? (50) | Catégorie libre |
| ExpenseType | ExpenseType? | Reimbursement ou PersonalConsumption |
| TeamMemberId | int? | CE → TeamMember (null = dépense d'organisation) |
| OrganizationPaymentSource | string? | "OrganizationCard" ou "OrganizationCash" |
| ActivityId | int | CE → Activity |
| ExpenseDate | DateTime | |

**Calcul du salaire :** `TotalToPay = (Jours × Indemnité journalière) + Remboursements − Consommations personnelles`

### EmailSent
Journal d'audit des emails envoyés.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| ActivityId | int? | CE → Activity |
| RecipientType | EmailRecipient | AllParents / ActivityGroup / MedicalSheetReminder |
| RecipientGroupId | int? | Défini si RecipientType = ActivityGroup |
| ScheduledDayId | int? | Filtre de jour appliqué lors de l'envoi |
| RecipientEmails | string | Séparé par des virgules |
| Subject | string (255) | |
| Message | string (5000) | Peut contenir des variables `%variable%` |
| SendSeparateEmailPerChild | bool | 1 email/enfant vs 1 email/parent |
| AttachmentFileName / AttachmentFilePath | string? | |
| SentDate | DateTime | |

### EmailTemplate
Template d'email réutilisable avec support des variables de fusion.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| OrganisationId | int | CE → Organisation |
| Name | string (200) | |
| TemplateType | EmailTemplateType | |
| Subject | string (500) | Peut contenir des variables |
| HtmlContent | string (10000) | Corps HTML avec des variables `%variable%` |
| IsDefault | bool | Template par défaut pour ce type |
| IsShared | bool | Visible par tous les coordinateurs |
| CreatedByUserId | string | CE → CedevaUser |
| CreatedDate / LastModifiedDate | DateTime | |

### BelgianMunicipality
Table de référence pour la validation et l'autocomplétion des codes postaux / villes.

| Champ | Type | Notes |
|-------|------|-------|
| Id | int | CP |
| PostalCode | string | Code postal belge |
| City | string | Nom de la commune |

### CedevaUser
Étend `IdentityUser` (ASP.NET Core Identity).

| Champ | Type | Notes |
|-------|------|-------|
| *(hérité)* | — | Id, UserName, Email, PasswordHash… |
| OrganisationId | int? | CE → Organisation |
| Role | Role | Admin / Coordinator |

---

## Diagramme des relations

```
Organisation (1) ──┬── (*) Activity ──┬── (*) ActivityDay ──── (*) BookingDay
                   │                  │
                   │                  ├── (*) ActivityGroup
                   │                  │
                   │                  ├── (*) ActivityQuestion ── (*) ActivityQuestionAnswer
                   │                  │
                   │                  ├── (*) Booking ──┬── (*) BookingDay
                   │                  │                 ├── (*) Payment
                   │                  │                 └── (1) Child
                   │                  │
                   │                  └── (*) Expense
                   │
                   ├── (*) Parent ──── (*) Child
                   │
                   ├── (*) TeamMember ── (*) Expense
                   │                  └── (*) Activity  (many-to-many)
                   │
                   ├── (*) EmailTemplate
                   │
                   └── (*) CedevaUser

Address (1) ── Organisation | Parent | TeamMember   (partagée, un-à-un chacune)
Activity (*) ←──→ (*) Child                         (many-to-many)
Activity (*) ←──→ (*) TeamMember                    (many-to-many via ActivityTeamMembers)
```

---

## Fonctionnalités principales

### Inscription publique (formulaire intégrable)
Formulaire sur une seule page, intégré par iframe au niveau de l'activité. Point d'entrée : `/PublicRegistration/Register?activityId={id}`.

Le formulaire collecte sur une seule page :
- Informations parent (détection de doublons par email — met à jour l'enregistrement existant)
- Informations enfant (détection de doublons par numéro de registre national — met à jour l'enregistrement existant)
- Questions personnalisées (si configurées pour l'activité)

À la soumission : tous les jours actifs sont automatiquement réservés, une communication structurée est générée et un email de confirmation est envoyé via Brevo.

Code d'intégration (pour coordinateurs/admin) : `/PublicRegistration/EmbedCode?activityId={id}`

### Hub de gestion d'activité
Gestion centralisée d'une activité (sélection basée sur la session) :
- **Dashboard** — Cartes d'action (réservations, présences, emails, équipe…)
- **UnconfirmedBookings** — Confirmer et assigner des groupes
- **Presences** — Suivi des présences avec vue par groupe
- **SendEmail** — Emails ciblés avec filtre de jour, variables de fusion, templates
- **SentEmails** — Journal d'audit des emails
- **TeamMembers** — Assigner / désassigner du personnel

### Module financier
- Vue unifiée des transactions (paiements + dépenses, codifiées par couleur)
- Workflow de paiement manuel (virement / cash / autre)
- Calcul du salaire des animateurs : prestations + remboursements − consommations personnelles
- Export Excel pour les salaires et les rapports financiers

### Paiement en ligne (Stripe)
Paiement en ligne agnostique du fournisseur, derrière `IPaymentGateway` :
- **Checkout** — `OnlinePaymentController` (anonyme) redirige vers Stripe Checkout (page hébergée) pour le solde dû ; un bouton « Payer en ligne » apparaît sur la page de confirmation publique quand `TotalAmount − PaidAmount > 0`.
- **Webhook** — un webhook Stripe signé applique le paiement à la réservation (`Payment(Online)`, mise à jour `PaidAmount`/`PaymentStatus`), idempotent sur la référence fournisseur.
- **Config** — `Stripe:SecretKey` / `Stripe:WebhookSecret` (Azure `Stripe__*`), jamais committés.
- Voir [docs/adr/0010](docs/adr/0010-online-payments-provider-agnostic-stripe.md).

### Gestion des présences
Suivi quotidien des présences à `/Presence` :
- Grille de cartes d'activités → sélection du jour → liste interactive (AJAX)
- Format imprimable A4 avec cases à signer

### Service d'email (Brevo)
- Emails de confirmation de réservation et de bienvenue
- SendEmail avec : filtre de destinataires (tous / groupe / rappel fiche médicale), filtre de jour, mode par enfant ou par parent
- Variables de fusion : `%prenom_enfant%`, `%nom_organisation%`, `%numero_compte%`, etc.
- Templates d'email réutilisables (CRUD) avec éditeur HTML

### Localisation
Trois langues (FR / NL / EN). Préférence par cookie.
- Fichiers de ressources : `Localization/SharedResources.{culture}.resx`
- Vues : `@Localizer["Key"]`
- Validation : DataAnnotationsLocalization lié à SharedResources

> **Critique :** Ne PAS définir `ResourcesPath` dans `Program.cs`. Bug connu d'ASP.NET qui casse la localisation des ressources partagées lorsque ResourcesPath est défini.

---

## Base de données

### Migrations
```bash
dotnet ef migrations add Nom --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update   --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
```

### Seeder de données de test
Se lance automatiquement au démarrage. Seeder par organisation :
- 25 parents, 50 enfants (communes belges réelles pour les adresses)
- 12 membres d'équipe (67% indemnisés, numéros de registre national valides)
- 4 activités avec jours, groupes, questions
- ~47 réservations avec communication structurée + distribution des statuts de paiement
- 28 paiements (BankTransfer + Cash) avec PaidAmount correct
- 20 dépenses par organisation (remboursements, consommations personnelles, carte/espèces organisation)
- 4 templates d'email (BookingConfirmation, PaymentReminder, MedicalSheetReminder, Custom)

### Multi-tenancy
Filtres de requête globaux sur toutes les entités par tenant. L'admin contourne via `IgnoreQueryFilters()`. Le seeder utilise toujours `IgnoreQueryFilters()`.

---

## Notes de développement

### Pièges fréquents
- **Child.BirthDate** — la propriété est `BirthDate`, pas `DateOfBirth`
- **Booking.IsConfirmed** — drapeau booléen, pas une énumération de statut
- **TeamMember.TeamMemberId** — la CP est `TeamMemberId`, pas `Id`
- **PostalCode** — type `string` (était `int` ; changé pour les codes postaux internationaux comme "1234AB")
- **IgnoreQueryFilters()** — retourne `IQueryable<T>`, pas `DbSet<T>`. Utiliser `FirstOrDefaultAsync(predicate)` au lieu de `FindAsync()`
- **Sérialisation TempData** — Toujours appeler `.ToString()` sur `LocalizedString` avant de stocker dans TempData
- **ResourcesPath dans Program.cs** — Ne PAS le définir. Voir le bug ASP.NET : https://github.com/aspnet/Localization/issues/268

### Conventions de nommage
```
PascalCase :  Classes, méthodes, propriétés
camelCase :   Variables locales, paramètres
_camelCase :  Champs privés
```

### Communication structurée belge
Format : `+++XXX/XXXX/XXXXX+++` — nombre de 10 chiffres, les 2 derniers chiffres = checksum mod-97 des 8 premiers. Utilisée comme référence de paiement sur les virements belges.
