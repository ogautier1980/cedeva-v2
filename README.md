# Cedeva

**Cedeva** is an ASP.NET Core MVC application for managing children's vacation activity centers in Belgium. Multi-tenant SaaS where organisations manage activities, child registrations, team members, parent communications, and financial tracking.

---

## Quick Start

```bash
docker-compose up -d                                          # Start SQL Server
dotnet run --project src/Cedeva.Website                       # Run (auto-seeds DB)
```

| Account | Email | Password |
|---------|-------|----------|
| Admin | admin@cedeva.be | Admin@123456 |
| Coordinator (Org 1) | coordinator@cedeva.be | Coord@123456 |
| Coordinator (Org 2) | coordinator.liege@cedeva.be | Coord@123456 |

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET Core MVC (.NET 9) |
| Database | SQL Server 2022 (Docker) |
| ORM | Entity Framework Core 9 |
| Email | Brevo SDK (C#) |
| Excel | ClosedXML |
| File Storage | Azure Blob Storage |
| DI Container | Autofac |
| Logging | Serilog |
| Auth | ASP.NET Core Identity |
| Localisation | FR / NL / EN (resource files) |
| Frontend | Bootstrap 5 + jQuery + FontAwesome |

---

## Architecture

### Project Layout

```
src/
├── Cedeva.Website/            # MVC presentation layer
│   ├── Features/              # Feature-based folder structure
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
│   │   └── Shared/            # Reusable partials (_AlertMessages, _Pagination, etc.)
│   ├── Localization/          # SharedResources.{fr,nl,en}.resx
│   └── wwwroot/               # Static assets (css/cedeva.css)
├── Cedeva.Core/               # Domain layer
│   ├── Entities/              # EF Core entities
│   ├── Interfaces/            # Service + repository interfaces
│   └── Enums/                 # All enumerations
└── Cedeva.Infrastructure/     # Infrastructure layer
    ├── Data/                  # DbContext, migrations, seeder
    ├── Services/              # Email, Excel, PDF, storage, CODA, reconciliation
    └── Repositories/          # Generic repository + unit of work
```

### Key Patterns

- **Multi-tenancy** — `OrganisationId` filtering via EF Core global query filters. Admin bypasses filters. Seeder uses `.IgnoreQueryFilters()`.
- **Feature folders** — Each feature is self-contained: controller + views + view models in one folder.
- **Repository + Unit of Work** — `IRepository<T>` / `IUnitOfWork`. Methods: `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
- **Localisation** — Cookie-based language selection. All views inject `IStringLocalizer<SharedResources>`. Keys follow convention: `Field.X`, `Button.X`, `Message.X`, `Enum.X.Value`.
- **TempData alerts** — Standardised keys: `SuccessMessage`, `ErrorMessage`, `WarningMessage`. Rendered via `_AlertMessages.cshtml` partial.

---

## Enumerations

| Enum | Values |
|------|--------|
| **Country** | Belgium (0), France (1), Netherlands (2), Luxembourg (3) |
| **Role** | Admin (0), Coordinator (1) |
| **TeamRole** | Animator (0), Coordinator (1) |
| **License** | License (0), Assimilated (1), Internship (2), Training (3), NoLicense (4) |
| **Status** | Compensated (0), Volunteer (1) |
| **QuestionType** | Text (0), Checkbox (1), Radio (2), Dropdown (3) |
| **EmailRecipient** | AllParents (0), ActivityGroup (1), MedicalSheetReminder (2) |
| **PaymentMethod** | BankTransfer (0), Cash (1), Other (2) |
| **PaymentStatus** | NotPaid (0), PartiallyPaid (1), Paid (2), Overpaid (3), Cancelled (4) |
| **ExpenseType** | Reimbursement (0), PersonalConsumption (1) |
| **TransactionType** | Income (0), Expense (1) |
| **TransactionCategory** | Payment (0), TeamExpense (1), PersonalConsumption (2), Other (3) |
| **EmailTemplateType** | BookingConfirmation (1), WelcomeEmail (2), MedicalSheetReminder (3), PaymentReminder (4), ActivityCancellation (5), Custom (99) |

---

## Entities

### Organisation
Multi-tenant root. Every entity is scoped to an organisation.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| Name | string (100) | Required |
| Description | string (500) | Required |
| AddressId | int | FK → Address |
| LogoUrl | string? | Azure Blob URL |
| BankAccountNumber | string? | IBAN for payment tracking |
| BankAccountName | string? | Account holder name |

### Activity
Vacation stage / programme managed by an organisation.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| Name | string (100) | Required |
| Description | string (500) | Required |
| IsActive | bool | |
| PricePerDay | decimal? | Used to calculate booking totals |
| StartDate / EndDate | DateTime | Date only |
| OrganisationId | int | FK → Organisation |
| Days | List\<ActivityDay> | Auto-generated days |
| Groups | ICollection\<ActivityGroup> | |
| TeamMembers | ICollection\<TeamMember> | Many-to-many |
| Bookings | ICollection\<Booking> | |

### ActivityDay
Represents a single calendar day within an activity.

| Field | Type | Notes |
|-------|------|-------|
| DayId | int | PK |
| Label | string (100) | e.g. "Lundi 18/03" |
| DayDate | DateTime | |
| Week | int? | Week number within activity |
| IsActive | bool | |
| ActivityId | int | FK → Activity |

### ActivityGroup
Named group within an activity (e.g. age-based grouping).

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| Label | string (100) | |
| Capacity | int? | Max children |
| ActivityId | int? | FK → Activity |

### ActivityQuestion / ActivityQuestionAnswer
Custom questions per activity. Answers stored per booking.

| ActivityQuestion | Type | Notes |
|-----------------|------|-------|
| Id | int | PK |
| ActivityId | int | FK → Activity |
| QuestionText | string (500) | |
| QuestionType | QuestionType | Text / Checkbox / Radio / Dropdown |
| IsRequired | bool | |
| Options | string? | Comma-separated for Radio/Dropdown |

| ActivityQuestionAnswer | Type | Notes |
|------------------------|------|-------|
| Id | int | PK |
| BookingId | int | FK → Booking |
| ActivityQuestionId | int | FK → ActivityQuestion |
| AnswerText | string (1000) | |

### Address
Shared address entity (Organisation, Parent, TeamMember).

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| Street | string (2–100) | |
| City | string (2–100) | |
| PostalCode | string (10) | Was int, changed to string for international support |
| Country | Country | Default: Belgium |

### Parent
Guardian / contact person. One parent can have multiple children.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| FirstName / LastName | string (2–100) | |
| Email | string (100) | EmailAddress validation |
| PhoneNumber | string? (100) | Belgian landline regex |
| MobilePhoneNumber | string (100) | Belgian mobile regex |
| NationalRegisterNumber | string (11–15) | Belgian national register |
| AddressId | int | FK → Address |
| OrganisationId | int | FK → Organisation |
| FullName | computed | "LastName, FirstName" |

### Child
Registered child. Links to Parent and can have multiple Bookings.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| FirstName / LastName | string (2–100) | |
| **BirthDate** | DateTime | ⚠️ Property name is `BirthDate` (not DateOfBirth) |
| NationalRegisterNumber | string (11–15) | |
| IsDisadvantagedEnvironment | bool | |
| IsMildDisability | bool | |
| IsSevereDisability | bool | |
| ParentId | int | FK → Parent |
| FullName | computed | "LastName, FirstName" |

### TeamMember
Staff member (animator or coordinator). Uses `TeamMemberId` as PK.

| Field | Type | Notes |
|-------|------|-------|
| **TeamMemberId** | int | PK (not `Id`) |
| FirstName / LastName | string (100) | |
| Email | string (100) | |
| BirthDate | DateTime | |
| MobilePhoneNumber | string (100) | |
| NationalRegisterNumber | string (11–15) | |
| TeamRole | TeamRole | Animator / Coordinator |
| License | License | Brevet type |
| Status | Status | Compensated / Volunteer |
| DailyCompensation | decimal? | Daily pay rate |
| LicenseUrl | string (100) | Azure Blob URL |
| AddressId | int | FK → Address |
| OrganisationId | int | FK → Organisation |
| Activities | ICollection\<Activity> | Many-to-many |

### Booking
Child registration for an activity. Central financial entity.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| BookingDate | DateTime | |
| ChildId | int | FK → Child |
| ActivityId | int | FK → Activity |
| GroupId | int? | FK → ActivityGroup |
| **IsConfirmed** | bool | ⚠️ Boolean flag (not a Status enum) |
| IsMedicalSheet | bool | Medical form received |
| StructuredCommunication | string? | Belgian format `+++XXX/XXXX/XXXXX+++` (mod-97) |
| TotalAmount | decimal | PricePerDay × reserved days |
| PaidAmount | decimal | Sum of payments |
| PaymentStatus | PaymentStatus | NotPaid / PartiallyPaid / Paid / Overpaid |
| Days | ICollection\<BookingDay> | |
| Payments | ICollection\<Payment> | |
| QuestionAnswers | ICollection\<ActivityQuestionAnswer> | |

### BookingDay
Links a booking to a specific activity day.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| ActivityDayId | int | FK → ActivityDay |
| IsReserved | bool | Day is booked |
| IsPresent | bool | Child attended |
| BookingId | int | FK → Booking |

### Payment
Individual payment record for a booking.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| BookingId | int | FK → Booking |
| Amount | decimal | |
| PaymentDate | DateTime | |
| PaymentMethod | PaymentMethod | BankTransfer / Cash / Other |
| Status | PaymentStatus | |
| StructuredCommunication | string? | For CODA matching |
| Reference | string? | Free-text reference |
| BankTransactionId | int? | FK → BankTransaction (set after CODA reconciliation) |
| CreatedByUserId | int? | User who registered manual payment |

### CodaFile
Imported Belgian bank statement (CODA format).

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| OrganisationId | int | FK → Organisation |
| FileName | string | Original file name |
| ImportDate | DateTime | When imported |
| StatementDate | DateTime | Bank statement date |
| AccountNumber | string | Organisation's bank account |
| OldBalance / NewBalance | decimal | Statement balances |
| TransactionCount | int | Number of transactions |
| ImportedByUserId | int | User who imported |
| Transactions | ICollection\<BankTransaction> | |

### BankTransaction
Single transaction from a CODA import. Reconciled against Payments.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| OrganisationId | int | FK → Organisation |
| TransactionDate / ValueDate | DateTime | |
| Amount | decimal | Negative for debits |
| StructuredCommunication | string? | Used for auto-matching |
| FreeCommunication | string? | |
| CounterpartyName / CounterpartyAccount | string? | |
| TransactionCode | string | CODA code (e.g. "05" = virement) |
| CodaFileId | int | FK → CodaFile |
| IsReconciled | bool | |
| PaymentId | int? | FK → Payment (after reconciliation) |

### Expense
Financial expense linked to an activity. Two categories: team-member expenses and organisation expenses.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| Label | string (100) | |
| Description | string? (500) | |
| Amount | decimal | |
| Category | string? (50) | Free-text category |
| ExpenseType | ExpenseType? | Reimbursement or PersonalConsumption |
| TeamMemberId | int? | FK → TeamMember (null = organisation expense) |
| OrganizationPaymentSource | string? | "OrganizationCard" or "OrganizationCash" |
| ActivityId | int | FK → Activity |
| ExpenseDate | DateTime | |

**Salary calculation:** `TotalToPay = (Days × DailyCompensation) + Reimbursements − PersonalConsumptions`

### EmailSent
Audit log of sent emails.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| ActivityId | int? | FK → Activity |
| RecipientType | EmailRecipient | AllParents / ActivityGroup / MedicalSheetReminder |
| RecipientGroupId | int? | Set when RecipientType = ActivityGroup |
| ScheduledDayId | int? | Day filter applied during send |
| RecipientEmails | string | Comma-separated |
| Subject | string (255) | |
| Message | string (5000) | May contain `%variable%` merge fields |
| SendSeparateEmailPerChild | bool | 1 email/child vs 1 email/parent |
| AttachmentFileName / AttachmentFilePath | string? | |
| SentDate | DateTime | |

### EmailTemplate
Reusable email template with merge-field variable support.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| OrganisationId | int | FK → Organisation |
| Name | string (200) | |
| TemplateType | EmailTemplateType | |
| Subject | string (500) | May contain variables |
| HtmlContent | string (10000) | HTML body with `%variable%` placeholders |
| IsDefault | bool | Default template for this type |
| IsShared | bool | Visible to all coordinators |
| CreatedByUserId | string | FK → CedevaUser |
| CreatedDate / LastModifiedDate | DateTime | |

### BelgianMunicipality
Reference table for postal code / city validation and autocomplete.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| PostalCode | string | Belgian postal code |
| City | string | Municipality name |

### CedevaUser
Extends `IdentityUser` (ASP.NET Core Identity).

| Field | Type | Notes |
|-------|------|-------|
| *(inherited)* | — | Id, UserName, Email, PasswordHash… |
| OrganisationId | int? | FK → Organisation |
| Role | Role | Admin / Coordinator |

---

## Entity Relationship Diagram

```
Organisation (1) ──┬── (*) Activity ──┬── (*) ActivityDay ──── (*) BookingDay
                   │                  │
                   │                  ├── (*) ActivityGroup
                   │                  │
                   │                  ├── (*) ActivityQuestion ── (*) ActivityQuestionAnswer
                   │                  │
                   │                  ├── (*) Booking ──┬── (*) BookingDay
                   │                  │                 ├── (*) Payment ── (0..1) BankTransaction
                   │                  │                 └── (1) Child
                   │                  │
                   │                  └── (*) Expense
                   │
                   ├── (*) Parent ──── (*) Child
                   │
                   ├── (*) TeamMember ── (*) Expense
                   │                  └── (*) Activity  (many-to-many)
                   │
                   ├── (*) CodaFile ── (*) BankTransaction
                   │
                   ├── (*) EmailTemplate
                   │
                   └── (*) CedevaUser

Address (1) ── Organisation | Parent | TeamMember   (shared, one-to-one each)
Activity (*) ←──→ (*) Child                         (many-to-many)
Activity (*) ←──→ (*) TeamMember                    (many-to-many via ActivityTeamMembers)
```

---

## Key Features

### Public Registration (Embeddable Form)
Single-page form embedded per activity via iframe. Entry point: `/PublicRegistration/Register?activityId={id}`.

The form collects on one page:
- Parent info (duplicate detected by email — updates existing record)
- Child info (duplicate detected by national register number — updates existing record)
- Custom questions (if configured for the activity)

On submit: all active days are automatically reserved, a structured communication is generated, and a confirmation email is sent via Brevo.

Embed code (for coordinators/admin): `/PublicRegistration/EmbedCode?activityId={id}`

### ActivityManagement Hub
Centralised management for a single activity (session-based selection):
- **Dashboard** — Action cards (bookings, presences, emails, team…)
- **UnconfirmedBookings** — Confirm + assign groups
- **Presences** — Attendance with group view
- **SendEmail** — Targeted emails with day filter, merge variables, templates
- **SentEmails** — Email audit log
- **TeamMembers** — Assign/unassign staff

### Financial Module
- Unified transaction view (payments + expenses, colour-coded)
- CODA import + automatic reconciliation by structured communication
- Manual cash payment workflow
- Team salary calculation: prestations + reimbursements − personal consumptions
- Excel export for salaries and financial reports

### Presence Management
Daily attendance at `/Presence`:
- Activity card grid → day selection → interactive checklist (AJAX)
- Printable A4 format with signature boxes

### Email Service (Brevo)
- Booking confirmation and welcome emails
- SendEmail with: recipient filter (all / group / medical reminder), day filter, per-child or per-parent mode
- Merge variables: `%prenom_enfant%`, `%nom_organisation%`, `%numero_compte%`, etc.
- Reusable email templates (CRUD) with HTML editor

### Localisation
Three languages (FR / NL / EN). Cookie-based preference.
- Resource files: `Localization/SharedResources.{culture}.resx`
- Views: `@Localizer["Key"]`
- Validation: DataAnnotationsLocalization bound to SharedResources

> **Critical:** Do NOT set `ResourcesPath` in `Program.cs`. Known ASP.NET bug breaks shared resource localisation when ResourcesPath is set.

---

## Database

### Migrations
```bash
dotnet ef migrations add Name --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update  --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
```

### Test Data Seeder
Auto-runs on startup. Seeds per organisation:
- 25 parents, 50 children (real Belgian municipalities for addresses)
- 12 team members (67% compensated, valid national register numbers)
- 4 activities with days, groups, questions
- ~47 bookings with structured communication + payment status distribution
- 28 payments (BankTransfer + Cash) with correct PaidAmount
- 1 CODA file with 15–19 bank transactions in 4 states: reconciled, matchable, unmatched credits, unmatched debits
- 20 expenses per org (reimbursements, personal consumptions, org card/cash)
- 4 email templates (BookingConfirmation, PaymentReminder, MedicalSheetReminder, Custom)

### Multi-Tenancy
Global query filters on all tenant-scoped entities. Admin bypasses via `IgnoreQueryFilters()`. Seeder always uses `IgnoreQueryFilters()`.

---

## Development Notes

### Common Gotchas
- **Child.BirthDate** — property is `BirthDate`, not `DateOfBirth`
- **Booking.IsConfirmed** — boolean flag, not a status enum
- **TeamMember.TeamMemberId** — PK is `TeamMemberId`, not `Id`
- **PostalCode** — `string` type (was `int`; changed for international codes like "1234AB")
- **IgnoreQueryFilters()** — returns `IQueryable<T>`, not `DbSet<T>`. Use `FirstOrDefaultAsync(predicate)` instead of `FindAsync()`
- **TempData serialisation** — Always `.ToString()` on `LocalizedString` before storing in TempData
- **Localisation ResourcesPath** — Do NOT set it. See ASP.NET bug: https://github.com/aspnet/Localization/issues/268

### Naming Conventions
```
PascalCase:  Classes, methods, properties
camelCase:   Local variables, parameters
_camelCase:  Private fields
```

### Belgian Structured Communication
Format: `+++XXX/XXXX/XXXXX+++` — 10-digit number, last 2 digits = mod-97 checksum of first 8. Used for CODA bank reconciliation.
