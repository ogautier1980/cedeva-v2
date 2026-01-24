# Cedeva - Spécification du Projet

## Vue d'ensemble

Cedeva est une application web ASP.NET Core de **gestion de centres d'activités et de stages de vacances pour enfants** en Belgique. Elle permet aux organisations de gérer leurs activités, les inscriptions des enfants, les équipes d'animation, et la communication avec les parents. L'application est déployée dans un container Docker.

## Contexte

- Chaque **organisation** gère ses propres stages pour enfants pendant les vacances scolaires
- Chaque organisation a accès uniquement à ses données (stages, enfants, parents, équipe...)
- Un **admin** a accès aux informations de toutes les organisations

## Objectifs fonctionnels

### Pour les organisations (Coordinateurs)
- Diffuser un **formulaire d'inscription** (iframe) intégrable sur leur propre site web
- Formulaire de base + questions personnalisables (text, checkbox, radio, dropdown)
- Gérer les réservations (depuis l'iframe ou encodage manuel)
- Gérer les **présences** jour par jour (impression de listes par groupe)
- Envoyer des **emails** aux parents via Brevo (individuel, par activité, par groupe, rappel fiche médicale)
- Gérer les **paiements** (montants, statut payé/non payé)
- Gérer les **animateurs et coordinateurs** (présences, paiement, remboursement frais)
- CRUD complet sur toutes les entités avec listes filtrables, triables et paginées

### Pour les admins
- Toutes les fonctionnalités coordinateur
- CRUD sur les organisations et utilisateurs

---

## Stack Technique

| Composant | Technologie |
|-----------|-------------|
| Backend | ASP.NET Core MVC (.NET 9, C#) |
| Base de données | SQL Server 2022 (Azure SQL en production) |
| ORM | Entity Framework Core 9 |
| Email | Brevo SDK C# |
| Excel | ClosedXML (import/export) |
| Stockage fichiers | Azure Blob Storage |
| Containerisation | Docker + Docker Compose |
| Localisation | FR / EN / NL |
| Frontend | HTML/JS/CSS + jQuery + Bootstrap 5 + FontAwesome |
| DI Container | Autofac |
| Logging | Serilog |
| Authentification | ASP.NET Core Identity |

---

## Décisions d'architecture

### Multi-tenancy
- **Approche** : Filtrage par `OrganisationId` (base de données unique)
- Toutes les requêtes EF Core sont automatiquement filtrées par l'organisation de l'utilisateur connecté
- L'admin peut voir toutes les organisations

### Stockage des fichiers
- **Logos organisations** : Azure Blob Storage
- **Brevets animateurs** : Azure Blob Storage
- Container : `cedeva-files` avec sous-dossiers par organisation

### Paiements
- **Phase 1** : Suivi manuel (statut payé/non payé, montant dû/reçu)
- **Phase 2** (future) : Intégration prestataire de paiement (Stripe/Mollie)

### Emails
- **Service** : Brevo (ex-SendinBlue)
- Configuration via `appsettings.json` (clé API en secrets)

---

## Architecture

### Organisation par Feature

Structure basée sur les features sous `Cedeva.Website/Features/`. Chaque feature contient :
- Controllers
- Views (Razor .cshtml)
- ViewModels
- Logique spécifique

### Convention de localisation des vues

```
/Features/{Controller}/{ViewName}.cshtml
/Features/Shared/{ViewName}.cshtml
/Features/Shared/{Controller}/{ViewName}.cshtml
```

### Configuration

Fichiers de configuration par environnement :
- `appsettings.json` (base)
- `appsettings.Development.json`
- `appsettings.Staging.json`
- `appsettings.Production.json`

---

## Localisation

L'application supporte 3 langues (FR, NL, EN) :
- **RequestLocalizationMiddleware** : détection via routes, cookies, headers
- **Route avec culture** : URLs incluent le code culture (`/fr/`, `/nl/`, `/en/`)
- **Fichiers de ressources** : `Cedeva.Website/Localization/Resources`
- **Localiseurs partagés** : `SharedViewLocalizer` et `SharedViewModelLocalizer`

---

## Design

- **Framework** : Bootstrap 5 avec thème admin moderne
- **Style** : Interface épurée, professionnelle
- **Référence** : https://bework.be/cedeva/tableau-de-bord.html (à adapter)
- **Composants** : Sidebar navigation, cards, datatables avec filtres/tri/pagination

---

## Énumérations

### Country
| Valeur | Code |
|--------|------|
| Belgium | 0 |
| France | 1 |
| Netherlands | 2 |
| Luxembourg | 3 |

### QuestionType
| Valeur | Code |
|--------|------|
| Text | 0 |
| Checkbox | 1 |
| Radio | 2 |
| Dropdown | 3 |

### EmailRecipient
| Valeur | Code |
|--------|------|
| AllParents | 0 |
| ActivityGroup | 1 |
| MedicalSheetReminder | 2 |

### TeamRole
| Valeur | Code |
|--------|------|
| Animator | 0 |
| Coordinator | 1 |

### License
| Valeur | Code |
|--------|------|
| License | 0 |
| Assimilated | 1 |
| Internship | 2 |
| Training | 3 |
| NoLicense | 4 |

### Status
| Valeur | Code |
|--------|------|
| Compensated | 0 |
| Volunteer | 1 |

### Role (Utilisateur)
| Valeur | Code |
|--------|------|
| Admin | 0 |
| Coordinator | 1 |

---

## Entités

### Organisation

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| Name | string | ✅ | max 100 | - |
| Description | string | ✅ | max 500 | - |
| AddressId | int | FK | - | FK → Address |
| Address | Address | ✅ | - | Navigation |
| LogoUrl | string | - | - | - |
| Activities | ICollection\<Activity> | - | - | Collection |

---

### Activity

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| Name | string | ✅ | max 100 | - |
| Description | string | ✅ | max 500 | - |
| IsActive | bool | ✅ | - | - |
| PricePerDay | decimal | - | - | - |
| StartDate | DateTime | ✅ | - | Date uniquement |
| EndDate | DateTime | ✅ | - | Date uniquement |
| OrganisationId | int | FK | - | FK → Organisation |
| Organisation | Organisation | - | - | Navigation |
| Days | List\<ActivityDay> | - | - | Collection |
| Children | ICollection\<Child> | - | - | Many-to-Many |
| TeamMembers | ICollection\<TeamMember> | - | - | Many-to-Many |
| Groups | ICollection\<ActivityGroup> | - | - | Collection |
| AdditionalQuestions | ICollection\<ActivityQuestion> | - | - | Collection |
| Bookings | ICollection\<Booking> | - | - | Collection |

---

### ActivityDay

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| DayId | int | PK | - | Clé primaire |
| Label | string | ✅ | max 100 | - |
| DayDate | DateTime | ✅ | - | Format: dd/MM/yyyy |
| Week | int | - | - | Numéro de semaine |
| IsActive | bool | ✅ | - | - |
| ActivityId | int | FK | - | FK → Activity |
| Activity | Activity | - | - | Navigation |
| BookingDays | List\<BookingDay> | - | - | Collection |

---

### ActivityGroup

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| Label | string | ✅ | max 100 | - |
| Capacity | int | - | - | Capacité max |
| ActivityId | int | FK | - | FK → Activity (nullable) |
| Activity | Activity | - | - | Navigation |
| Children | List\<Child> | - | - | Collection |

---

### ActivityQuestion

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| ActivityId | int | FK | - | FK → Activity |
| Activity | Activity | - | - | Navigation |
| QuestionText | string | ✅ | max 500 | - |
| QuestionType | QuestionType | - | - | Enum |
| IsRequired | bool | - | - | - |
| Options | string | - | - | "Option1,Option2,Option3" pour dropdown/radio |

---

### ActivityQuestionAnswer

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| BookingId | int | ✅ | - | FK → Booking |
| Booking | Booking | - | - | Navigation |
| ActivityQuestionId | int | ✅ | - | FK → ActivityQuestion |
| ActivityQuestion | ActivityQuestion | - | - | Navigation |
| AnswerText | string | ✅ | max 1000 | - |

---

### Address

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| Street | string | ✅ | min 2, max 100 | - |
| City | string | ✅ | min 2, max 100 | Validation commune belge |
| PostalCode | int | ✅ | - | Validation code postal belge |
| Country | Country | ✅ | - | Défaut: Belgium |

> **Validation** : `[BelgianPostalCodeCityValidation]` sur la classe

---

### BelgianMunicipality

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| PostalCode | int | - | - | - |
| City | string | - | - | - |

> Table de référence pour la validation des codes postaux belges
> **Source des données** : Fichier CSV/JSON fourni par le client

---

### Booking

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| BookingDate | DateTime | - | - | Date de réservation |
| Days | List\<BookingDay> | - | - | Collection |
| ChildId | int | ✅ | - | FK → Child |
| Child | Child | - | - | Navigation |
| ActivityId | int | ✅ | - | FK → Activity |
| Activity | Activity | - | - | Navigation |
| GroupId | int | - | - | FK → ActivityGroup (nullable) |
| Group | ActivityGroup | - | - | Navigation |
| IsConfirmed | bool | ✅ | - | Réservation confirmée |
| IsMedicalSheet | bool | ✅ | - | Fiche médicale reçue |

---

### BookingDay

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| ActivityDayId | int | ✅ | - | FK → ActivityDay |
| ActivityDay | ActivityDay | - | - | Navigation |
| IsReserved | bool | ✅ | - | Jour réservé |
| IsPresent | bool | ✅ | - | Enfant présent |
| BookingId | int | ✅ | - | FK → Booking |
| Booking | Booking | - | - | Navigation |

---

### Parent

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| FirstName | string | ✅ | min 2, max 100 | - |
| LastName | string | ✅ | min 2, max 100 | - |
| Email | string | ✅ | max 100 | Validation EmailAddress |
| AddressId | int | FK | - | FK → Address |
| Address | Address | ✅ | - | Navigation |
| PhoneNumber | string | - | max 100 | Téléphone fixe belge |
| MobilePhoneNumber | string | ✅ | max 100 | Mobile belge |
| NationalRegisterNumber | string | ✅ | min 11, max 15 | Numéro national belge |
| OrganisationId | int | FK | - | FK → Organisation |
| Organisation | Organisation | - | - | Navigation |
| Children | ICollection\<Child> | - | - | Collection |
| FullName | string | computed | - | "LastName, FirstName" |

**Regex validations :**
- PhoneNumber (fixe) : `^((\+32|0032)[\s\.\-\/]?|0)[\s\.\-\/]?\d([\s\.\-\/]?\d){7}$`
- MobilePhoneNumber : `^((\+32|0032)[\s\.\-\/]?|0)[\s\.\-\/]?4[789]([\s\.\-\/]?\d){7}$`
- NationalRegisterNumber : `^(\d{2})[.\- ]?(0[1-9]|1[0-2])[.\- ]?(0[1-9]|[12]\d|3[01])[.\- ]?(\d{3})[.\- ]?(\d{2})$`

---

### Child

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| FirstName | string | ✅ | min 2, max 100 | - |
| LastName | string | ✅ | min 2, max 100 | - |
| BirthDate | DateTime | ✅ | - | Date de naissance |
| NationalRegisterNumber | string | ✅ | min 11, max 15 | Numéro national belge |
| IsDisadvantagedEnvironment | bool | ✅ | - | Milieu défavorisé |
| IsMildDisability | bool | ✅ | - | Handicap léger |
| IsSevereDisability | bool | ✅ | - | Handicap lourd |
| ParentId | int | FK | - | FK → Parent |
| Parent | Parent | - | - | Navigation |
| Activities | ICollection\<Activity> | - | - | Many-to-Many |
| FullName | string | computed | - | "LastName, FirstName" |

---

### TeamMember

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| TeamMemberId | int | PK | - | Clé primaire |
| FirstName | string | ✅ | max 100 | - |
| LastName | string | ✅ | max 100 | - |
| Email | string | ✅ | max 100 | - |
| BirthDate | DateTime | ✅ | - | Date de naissance |
| AddressId | int | FK | - | FK → Address |
| Address | Address | ✅ | - | Navigation |
| MobilePhoneNumber | string | ✅ | max 100 | Mobile belge |
| NationalRegisterNumber | string | ✅ | min 11, max 15 | Numéro national belge |
| TeamRole | TeamRole | ✅ | - | Animator / Coordinator |
| License | License | ✅ | - | Type de brevet |
| Status | Status | ✅ | - | Compensated / Volunteer |
| DailyCompensation | decimal | - | - | Indemnité journalière |
| LicenseUrl | string | ✅ | max 100 | URL du document brevet |
| OrganisationId | int | FK | - | FK → Organisation |
| Organisation | Organisation | - | - | Navigation |
| Activities | ICollection\<Activity> | - | - | Many-to-Many |
| Expenses | ICollection\<Expense> | - | - | Collection |
| FullName | string | computed | - | "LastName, FirstName" |

---

### Expense

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| Label | string | ✅ | max 100 | Description du frais |
| Amount | decimal | ✅ | - | Montant |
| TeamMemberId | int | ✅ | - | FK → TeamMember |
| TeamMember | TeamMember | - | - | Navigation |
| ActivityId | int | FK | - | FK → Activity |
| Activity | Activity | - | - | Navigation |

---

### EmailSent

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| Id | int | PK | - | Clé primaire |
| ActivityId | int | FK | - | FK → Activity |
| Activity | Activity | - | - | Navigation |
| RecipientType | EmailRecipient | ✅ | - | Type de destinataires |
| RecipientGroupId | int | - | - | Si RecipientType = ActivityGroup |
| RecipientEmails | string | ✅ | - | Emails séparés par virgules |
| Subject | string | ✅ | max 255 | Sujet |
| Message | string | ✅ | max 1024 | Corps du message |
| AttachmentFileName | string | - | max 255 | Nom du fichier joint |
| AttachmentFilePath | string | - | max 500 | Chemin du fichier joint |
| SentDate | DateTime | ✅ | - | Date d'envoi |

---

### CedevaUser

Hérite de `IdentityUser` (ASP.NET Core Identity)

| Champ | Type | Required | Longueur | Notes |
|-------|------|----------|----------|-------|
| *(hérité)* | - | - | - | Id, UserName, Email, PasswordHash... |
| OrganisationId | int | FK | - | FK → Organisation |
| Organisation | Organisation | - | - | Navigation |
| Role | Role | ✅ | - | Défaut: Coordinator |

---

## Diagramme des Relations

```
Organisation (1) ──────┬────── (*) Activity
      │                │
      │                ├────── (*) ActivityDay ──── (*) BookingDay
      │                │
      │                ├────── (*) ActivityGroup
      │                │
      │                ├────── (*) ActivityQuestion ──── (*) ActivityQuestionAnswer
      │                │
      │                └────── (*) Booking ──┬── (*) BookingDay
      │                                      │
      │                                      └── (1) Child
      │
      ├────── (*) Parent ──── (*) Child
      │
      ├────── (*) TeamMember ──── (*) Expense
      │
      └────── (*) CedevaUser

Address (1) ──── (1) Organisation
Address (1) ──── (1) Parent
Address (1) ──── (1) TeamMember

Activity (*) ←───→ (*) Child (Many-to-Many)
Activity (*) ←───→ (*) TeamMember (Many-to-Many)
```

---

## Principes de développement

### Séparation des responsabilités
- **Controllers** : Gestion des requêtes HTTP uniquement
- **Services** : Logique métier
- **Repositories** : Accès aux données
- **Models** : Entités et DTOs
- **ViewModels** : Données pour les vues

### Conventions de nommage
```csharp
// Classes, Méthodes, Propriétés: PascalCase
public class UserService { }
public async Task<User> GetUserAsync(int id) { }

// Variables locales, paramètres: camelCase
var userId = 123;

// Champs privés: _camelCase
private readonly ILogger<UserService> _logger;
```

### Async/Await
- Toujours utiliser async/await pour I/O
- Suffixer les méthodes async avec `Async`

### Validation
- Valider à la frontière (controllers)
- Utiliser Data Annotations
- FluentValidation pour validation complexe
