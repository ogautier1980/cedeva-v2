# CLAUDE.md

Guide for Claude Code when working with the Cedeva codebase.

## ⚠️ Working Principles — READ FIRST

1. **Never claim without proof.** Say "I completed X in file Y at line Z", not "all done".
2. **Verify before stating.** Read files, run searches, test — don't assume.
3. **Diagnose before changing.** Investigate root cause first. Never make blind changes.
4. **Be specific.** Reference file paths and line numbers in every statement.
5. **Document what you actually did.** Commit messages list concrete changes.

---

## Project Overview

**Cedeva** — ASP.NET Core MVC (.NET 9) for managing children's vacation activity centers in Belgium. Multi-tenant SaaS (organisations scope all data). Full spec: [README.md](README.md).

**Stack:** .NET 9 · SQL Server 2022 · EF Core 9 · ASP.NET Identity · Bootstrap 5 · Docker · Brevo email · ClosedXML · Azure Blob Storage

## Quick Reference

### Commands
```bash
dotnet build
dotnet watch run --project src/Cedeva.Website
dotnet ef migrations add <Name> --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update  --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
docker-compose up -d
```

### Login (seeded)
| Role | Email | Password |
|------|-------|----------|
| Admin | admin@cedeva.be | Admin@123456 |
| Coordinator (Org 1) | coordinator@cedeva.be | Coord@123456 |
| Coordinator (Org 2) | coordinator.liege@cedeva.be | Coord@123456 |

### Key Files
| File | Purpose |
|------|---------|
| README.md | Complete technical spec, entity schemas, architecture |
| CLAUDE.md | This file — developer guidelines for Claude |
| Program.cs | Service registration, middleware pipeline |
| appsettings.json | Connection strings, Brevo API key, Azure keys |
| docker-compose.yml | SQL Server 2022 config |
| cedeva.css | Custom CSS (Cedeva colour palette overrides Bootstrap) |

---

## Architecture

```
src/
├── Cedeva.Website/        # MVC (feature folders under Features/)
│   ├── Features/          # One folder per feature (controller + views + ViewModels)
│   │   └── Shared/        # Reusable partials (_AlertMessages, _Pagination, _SortableColumnHeader)
│   └── Localization/      # SharedResources.{fr,nl,en}.resx  (~600 keys)
├── Cedeva.Core/           # Domain (entities, interfaces, enums)
└── Cedeva.Infrastructure/ # Data (DbContext, migrations, seeder), services
```

### Patterns
- **Multi-tenancy:** EF Core global query filters on `OrganisationId`. Admin bypasses. Seeder uses `.IgnoreQueryFilters()`.
- **Feature folders:** `/Features/{Feature}/{View}.cshtml`
- **Repository + UoW:** `IRepository<T>` with `IUnitOfWork`
- **Localisation:** `@Localizer["Key"]` everywhere. Cookie-based FR/NL/EN. **Do NOT set ResourcesPath** (ASP.NET bug).
- **TempData alerts:** Standardised keys `SuccessMessage` / `ErrorMessage` / `WarningMessage`. All views use `@await Html.PartialAsync("Shared/_AlertMessages")`.
- **DI:** Autofac. Services registered in `Program.cs` lines 39–52.

---

## Critical Gotchas

| Trap | Correct approach |
|------|-----------------|
| Child birth date | Property is `BirthDate` (not `DateOfBirth`) |
| Booking confirmation | `IsConfirmed` boolean (not a Status enum) |
| TeamMember PK | `TeamMemberId` (not `Id`) |
| PostalCode type | `string` (changed from int for international codes) |
| IgnoreQueryFilters() | Returns `IQueryable<T>` — use `FirstOrDefaultAsync(pred)`, not `FindAsync()` |
| TempData + LocalizedString | Call `.ToString()` before storing (serialisation fails otherwise) |
| ResourcesPath in Program.cs | Do NOT set it. Bug: https://github.com/aspnet/Localization/issues/268 |
| LINQ with local lists | Extract IDs first: `var ids = list.Select(x => x.Id).ToList()` then `ids.Contains(...)` |

---

## Implementation Status

### ✅ Completed
- Phases 1–4: Core CRUD (Activities, Bookings, Children, Parents, TeamMembers, Organisations, Users)
- Phase 5: Localisation infrastructure (FR working; NL/EN have placeholder translations with `[NL]`/`[EN]` prefixes)
- Phase 6: Financial module (payments, CODA import, reconciliation, expenses, team salaries, Excel export)
- ActivityManagement hub (dashboard, unconfirmed bookings, presences, email, team assignment)
- Public registration wizard (embeddable iframe, duplicate detection, Brevo confirmation)
- Presence management (daily attendance, printable lists)
- Email templates (CRUD, merge variables, HTML content)
- Sortable columns + pagination on all Index pages (reusable partials)
- Design consistency pass (CSS variable overrides for Bootstrap table colours, alert partial standardisation)
- Code clean-up: alert partial extraction (20 views), TempData key standardisation (4 controllers), ModelState dead-code removal (53 checks from GET actions)
- Test data seeder rewrite (full financial + CODA data)
- Excursions feature (Phases 1–12): CRUD, registrations, attendance, email, expenses, edit/delete/details, financial integration, seeder, NL/EN translations
- **Audit trail system:** CreatedAt/CreatedBy/ModifiedAt/ModifiedBy on all 24 entities (2026-02-06)
- **Postal code filtering:** Activities can include/exclude postal codes for public registration eligibility (2026-02-05)
- **Code quality improvements (2026-02-06):**
  - Security hardening: XSS fixes (JSON serialization), HttpClient DI pattern, path traversal protection
  - Financial calculation service: Extracted 7 calculation methods from controllers (-44% complexity)
  - Activity selection service: Centralized session/cookie management (-18% controller code)

### 🔄 In Progress
- Phase 7: UX improvements (postal code autocomplete, booking day cards, admin org selection)
- NL/EN translation completion

---

## Excursions — Entity Summary (2026-02-05)

| Entity | Key fields | Relations |
|--------|-----------|-----------|
| `Excursion` | Name, ExcursionDate, StartTime?, EndTime?, Cost, Type (enum), IsActive | → Activity, → ExcursionGroups, → Registrations, → TeamMembers, → Expenses |
| `ExcursionGroup` | ExcursionId, ActivityGroupId | Junction: Excursion ↔ ActivityGroup |
| `ExcursionRegistration` | ExcursionId, BookingId, RegistrationDate, IsPresent | Links Booking to Excursion; updates Booking.TotalAmount on register/unregister |
| `ExcursionTeamMember` | ExcursionId, TeamMemberId, IsAssigned, IsPresent | Staff assignment + attendance |
| `Expense` (extended) | ExcursionId? (nullable FK) | When set, expense belongs to an excursion |

**Service:** `IExcursionService` / `ExcursionService` — register/unregister children (mutates `Booking.TotalAmount`, creates `ActivityFinancialTransaction` audit records), update attendance, financial summary.

**Controller:** `ExcursionsController` — Index, Create, Edit, Delete, Details, Registrations (AJAX), Attendance (AJAX), Expenses (add form), SendEmail.

**Views:** `/Features/ActivityManagement/Excursions/` — Index, Create, Edit, Details, Delete, Registrations, Attendance, Expenses, SendEmail.

---

## Recent Changes (2026-02-06)

### Audit Trail System (2026-02-06)
- **AuditableEntity base class:** CreatedAt, CreatedBy, ModifiedAt, ModifiedBy properties for all domain entities
- **24 entities inherit** from AuditableEntity (Activity, Booking, Child, Parent, Organisation, TeamMember, Payment, Excursion, etc.)
- **CedevaUser exception:** Audit properties added directly (already inherits from IdentityUser)
- **SaveChangesAsync override:** Automatic population of audit fields on create/modify (CedevaDbContext.cs:97-149)
- **Migration:** AddAuditFieldsToAllEntities with SQL to initialize existing records (CreatedBy='System', CreatedAt=current timestamp)
- **ViewModels + Controllers:** 8 entities display user-friendly audit info (FirstName + LastName instead of UserId GUID)
- **Details views:** 9 views show audit information at bottom: "Créé par Admin Cedeva le 06/02/2026 à 00:25"
- **Localization:** Audit.CreatedBy, Audit.ModifiedBy, Audit.On, Audit.At keys (FR/NL/EN)
- **"System" user:** Used for iframe registrations and automated operations when no user context

### Postal Code Filtering (2026-02-05)
- **Activity entity:** IncludedPostalCodes and ExcludedPostalCodes properties (comma-separated lists)
- **Public registration:** Validates parent postal code against inclusion/exclusion lists
- **Migration:** AddPostalCodeInclusionExclusionToActivity
- **UI:** Activity Create/Edit forms include postal code fields with help text

### Security & Code Quality Improvements (2026-02-06)

**Security Hardening:**
- **XSS fixes:** Replaced `@Html.Raw()` with `@Json.Serialize()` for localized strings in JavaScript contexts (Organisations/Edit.cshtml, TeamMembers/Edit.cshtml)
- **XSS fix:** HTML-encode email messages before rendering with `<br/>` replacement (SentEmails.cshtml)
- **HttpClient pattern:** BrevoEmailService now uses IHttpClientFactory DI instead of creating new instances (fixes socket exhaustion risk)
- **Path traversal protection:** LocalFileStorageService validates paths don't contain `../` or escape WebRootPath
- **File validation:** Localized error messages (FileValidationAttributes.cs) with proper float division for file size display
- **.gitignore:** Added `src/Cedeva.Website/wwwroot/uploads/` to exclude user-uploaded files

**Service Extraction (Code Quality):**
- **FinancialCalculationService:** Extracted 7 calculation methods from FinancialController (Index: 25→14 lines, -44%; TeamSalaries: 7→1 lines, -86%)
  - Methods: CalculateTotalRevenue, CalculateOrganizationExpenses, CalculateTeamMemberSalaries, CalculateTeamMemberSalary, CalculateTotalExpenses, CalculatePendingPayments, CalculateNetProfit
  - Benefits: Reusable business logic, unit testable without controllers, centralized financial calculations
- **ActivitySelectionService:** Centralized session + cookie management for activity selection (ActivityManagementController: 990→814 lines, -18%)
  - Methods: GetSelectedActivityId, SetSelectedActivityId, ClearSelectedActivityId
  - Features: Session storage (temporary), cookie persistence (30 days), secure options (HttpOnly, Secure, SameSite=Lax)
  - Refactored: ActivityManagementController, ExcursionsController (~100+ lines of duplicate code removed)
  - Benefits: DRY principle, testable without HttpContext, consistent behavior across controllers

### Local File Storage (2026-02-06)
- **LocalFileStorageService:** IStorageService implementation for local development (saves to wwwroot/uploads/)
- **Configuration:** Program.cs registers LocalFileStorageService in Development, AzureBlobStorageService in Production

---

## Recent Changes (2026-02-05)

### Excursions Feature (2026-02-05)
- Entities: Excursion, ExcursionGroup, ExcursionRegistration, ExcursionTeamMember
- Migrations: `AddExcursions`, `AddExcursionTimingAndTeamMembers`
- ExcursionService with register/unregister (financial audit trail), attendance updates
- Full CRUD: create, edit (TimeSpan↔string), soft-delete (guards on registrations), details summary
- Registration management: AJAX checkboxes per group, payment status badges
- Attendance tracking: AJAX presence per group, summary cards
- Expenses: per-excursion expense list with add form (categories, payment source)
- Email: send to registered parents with group/all filter and merge variables
- Financial integration: excursion expenses show in Transactions view with yellow badge
- Seeder: 1-2 excursions per activity, 30-70% registration rate, expenses, team assignments
- Localisation: ~86 FR keys + NL/EN placeholders

## Recent Changes (2026-01-31 / 2026-02-01)

### Code Clean-Up (2026-02-01)
- Created `_AlertMessages.cshtml` partial — replaces duplicated alert blocks in 20 views
- Standardised TempData keys to `SuccessMessage` / `ErrorMessage` / `WarningMessage` across all controllers (4 controllers had inconsistent "Success"/"Error" keys)
- Removed 53 dead `if (!ModelState.IsValid) { return BadRequest(ModelState); }` blocks from GET actions — ModelState is never populated on GET requests, so these checks were unreachable code
- Note: SonarQube rule S6967 ("ModelState.IsValid should be checked") fires on GET actions after removal. This is a false positive — the rule doesn't distinguish GET vs POST.

### Test Data Seeder Rewrite (2026-02-01)
- Complete rewrite of `TestDataSeeder.cs` (~900 lines)
- Full financial data: payments, CODA files, bank transactions, expenses, email templates
- Belgian structured communication with mod-97 checksum
- Real postal codes/cities from BelgianMunicipalities

### Financial Module (2026-01-31)
- Unified transactions, colour-coded payments/expenses
- CODA import + auto-reconciliation
- Team salary calculations with Excel export
- All views localised

### TeamRole Bug Fix (2026-01-31)
- Fixed seeder overflow (`Next(0,4)` for 2-value enum) generating invalid TeamRole values
- Fixed missing `Enum.` prefix in localiser key in TeamSalaries view
- Cleaned stale DB data via SQL