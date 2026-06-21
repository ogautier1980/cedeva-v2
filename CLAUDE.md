# CLAUDE.md

Guide for Claude Code when working with the Cedeva codebase.

## ⚠️ Working Principles — READ FIRST

1. **Never claim without proof.** Say "I completed X in file Y at line Z", not "all done".
2. **Verify before stating.** Read files, run searches, test — don't assume.
3. **Diagnose before changing.** Investigate root cause first. Never make blind changes.
4. **Be specific.** Reference file paths and line numbers in every statement.
5. **Document what you actually did.** Commit messages list concrete changes.
6. **NEVER push without asking.** Always request permission before `git push`, even if it seems logical.
7. **Don't make absolute claims.** Avoid "this will work" or "Azure DevOps should compile" — you cannot guarantee external results.

---

## Project Overview

**Cedeva** — ASP.NET Core MVC (.NET 10) for managing children's vacation activity centers in Belgium. Multi-tenant SaaS (organisations scope all data). Full spec: [README.md](README.md).

**Stack:** .NET 10 · SQL Server 2022 · EF Core 10 · ASP.NET Identity · Bootstrap 5 · Docker · Brevo email · Stripe payments · ClosedXML · Azure Blob Storage

## Quick Reference

### Commands
```bash
dotnet build
dotnet watch run --project src/Cedeva.Website
dotnet ef migrations add <Name> --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
docker-compose up -d
```

### Login (seeded)
| Role | Email | Password |
|------|-------|----------|
| Admin | admin@cedeva.be | Admin@123456 |
| Coordinator (Org 1) | coordinator@cedeva.be | Coord@123456 |
| Coordinator (Org 2) | coordinator.liege@cedeva.be | Coord@123456 |

**Seeding:** `DbSeeder` always runs (roles, admin, 2 orgs + coordinators, Belgian municipalities).
`TestDataSeeder` (rich, idempotent, realistic Belgian-FR demo data across all tables) runs when
`SeedDemoData=true` — defaults to on in Development, off elsewhere. Set `SeedDemoData=true` as an
Azure app setting to populate the demo site. Disable all startup seeding with `RunStartupSeeding=false`.

### Key Files
| File | Purpose |
|------|---------|
| README.md | Complete technical spec, entity schemas, architecture |
| CLAUDE.md | This file — developer guidelines |
| Program.cs | Service registration, middleware pipeline |
| appsettings.json | Connection strings, API keys |
| cedeva.css | Custom CSS (Cedeva color palette) |

---

## Architecture

```
src/
├── Cedeva.Website/        # MVC (feature folders)
│   ├── Features/          # One folder per feature (controller + views + ViewModels)
│   │   └── Shared/        # Reusable partials (_AlertMessages, _Pagination, _SortableColumnHeader)
│   └── Localization/      # SharedResources.{fr,nl,en}.resx (~1300 keys each, full parity)
├── Cedeva.Core/           # Domain (entities, interfaces, enums)
└── Cedeva.Infrastructure/ # Data (DbContext, migrations, seeder), services
```

### Patterns
- **Multi-tenancy:** EF Core global query filters on `OrganisationId`. Admin bypasses via `.IgnoreQueryFilters()`.
- **Feature folders:** `/Features/{Feature}/{View}.cshtml`
- **Repository + UoW:** `IRepository<T>` with `IUnitOfWork`
- **Localisation:** `@Localizer["Key"]` everywhere. Cookie-based FR/NL/EN. **Do NOT set ResourcesPath** (ASP.NET bug).
  - **Validation messages:** DataAnnotations `ErrorMessage` must be a **resx key** (`Validation.*`, e.g. `Validation.Required`, `Validation.InvalidEmail`), never a literal — `AddDataAnnotationsLocalization` resolves it via SharedResources. Never leave a validation attribute without `ErrorMessage` (it falls back to the English framework default).
  - **Identity errors** (password policy, duplicate email…) are localized by `LocalizedIdentityErrorDescriber` (keys `Identity.*`). Controller error strings go through `_localizer`, never hardcoded.
- **TempData alerts:** Standardized keys: `SuccessMessage` / `ErrorMessage` / `WarningMessage`. Use `@await Html.PartialAsync("Shared/_AlertMessages")`.
- **DI:** Autofac. Services registered in `Program.cs`.

---

## Critical Gotchas

| Trap | Correct approach |
|------|-----------------|
| Child birth date | Property is `BirthDate` (not `DateOfBirth`) |
| Booking confirmation | `IsConfirmed` boolean (not Status enum) |
| TeamMember PK | `TeamMemberId` (not `Id`) |
| PostalCode type | `string` (not int — supports international codes) |
| IgnoreQueryFilters() | Returns `IQueryable<T>` — use `FirstOrDefaultAsync(pred)`, not `FindAsync()` |
| TempData + LocalizedString | Call `.ToString()` before storing (serialization fails otherwise) |
| ResourcesPath in Program.cs | Do NOT set it. Bug: https://github.com/aspnet/Localization/issues/268 |
| LINQ with local lists | Extract IDs first: `var ids = list.Select(x => x.Id).ToList()` then use `ids.Contains(...)` |
| National Register Number | Format: YY.MM.DD-XXX.XX (11 digits with formatting). Use `NationalRegisterNumberHelper.Format()` for display. |

---

## Key Features Implemented

### Core Features
- **CRUD modules:** Activities, Bookings, Children, Parents, TeamMembers, Organisations, Users
- **Localisation:** FR, NL, EN — all three fully translated (~1300 keys each, no `[NL]`/`[EN]` placeholders left; keys verified at parity across the 3 files)
- **Multi-tenancy:** Organisation-scoped data with admin bypass
- **Audit trail:** CreatedAt/CreatedBy/ModifiedAt/ModifiedBy on all 24 entities (auto-populated)

### Financial Module
- Payments, expenses tracking, team salary calculations
- Excel export, color-coded transactions
- `FinancialCalculationService` for reusable business logic
- ⚠️ CODA import & bank reconciliation were **removed** (replaced by online payments — see below)

### Online Payments (Stripe)
- Provider-agnostic `IPaymentGateway` (Checkout + webhook) with `StripePaymentGateway`
- `OnlinePaymentController` (anonymous): Checkout redirect, Return, signed Webhook
- `BookingPaymentService` applies a paid webhook to the booking (records `Payment(Online)`,
  updates `PaidAmount`/`PaymentStatus`, idempotent on provider reference)
- "Pay online" button on the public confirmation page (amount = remaining due)
- Secrets via config `Stripe:SecretKey` / `Stripe:WebhookSecret` (Azure `Stripe__*`) — never committed
- See [docs/adr/0010](docs/adr/0010-online-payments-provider-agnostic-stripe.md)

### Activity Management
- Dashboard, unconfirmed bookings, presences
- Email system with templates and merge variables
- Team assignment, postal code filtering
- `ActivitySelectionService` for session/cookie management

### Public Registration
- Embeddable iframe with customization (colors via query params)
- Duplicate detection, Brevo email confirmation
- Custom questions per activity

### Excursions
- CRUD with soft-delete, registration management
- Attendance tracking, per-excursion expenses
- Financial integration (updates `Booking.TotalAmount`)
- `ExcursionService` for business logic

---

## Recent Improvements (Feb 2026)

### Code Quality
- **Service extraction:** FinancialCalculationService, ActivitySelectionService, ExcursionViewModelBuilderService
- **Controller extensions:** Standardized TempData patterns, redirect helpers
- **Session management:** Strongly-typed SessionState wrapper

### Security
- XSS fixes (JSON serialization in JS contexts)
- HttpClient DI pattern (IHttpClientFactory)
- Path traversal protection
- File upload validation

### UX
- National register number formatting **and validation** (YY.MM.DD-XXX.XX + mod-97 check digit via
  `[ValidNationalRegisterNumber]`, applied across all parent/child/team-member forms)
- Email recipient grouping with separators
- Color pickers for iframe customization
- Clean URLs (route parameters vs query strings)

## Testing (June 2026)
- **~1175 unit/integration tests** (+ 65 E2E browser, 0 skipped; + 3 SQL Server), **≈92% line coverage** (branch ≈77%, method ≈95%); CI coverage gate at 85%.
- 5 levels across 3 projects: `Cedeva.Tests` (unit + service-integration SQLite + controller
  WebApplicationFactory), `Cedeva.Tests.Sql` (real SQL Server via Testcontainers), `Cedeva.Tests.E2E`
  (Playwright + Chromium). E2E and SQL run in dedicated CI workflows that do **not** gate deploy.
- Full guide + test-authoring gotchas: [docs/test-strategy.md](docs/test-strategy.md) and
  [docs/adr/0011](docs/adr/0011-test-layers-e2e-and-db-fidelity.md).

---

## Best Practices

### Do's ✅
- Read files before editing
- Use dedicated tools (Read/Edit/Write) instead of bash for file operations
- Reference file paths and line numbers
- Test changes before committing
- Use `ControllerExtensions` for TempData messages
- Use `NationalRegisterNumberHelper.Format()` for display

### Don'ts ❌
- Don't push to GitHub without explicit user permission
- Don't make absolute claims about external systems
- Don't use bash for file operations (use Read/Edit/Write tools)
- Don't set ResourcesPath in Program.cs
- Don't use `@Html.Raw()` with user input (XSS risk)
- Don't create HttpClient instances directly (use IHttpClientFactory)

---

## Common Tasks

### Adding a new entity
1. Create entity in `Cedeva.Core/Entities/` inheriting from `AuditableEntity`
2. Add DbSet to `CedevaDbContext`
3. Configure in `OnModelCreating` (include query filter for multi-tenancy if needed)
4. Create migration: `dotnet ef migrations add AddEntityName`
5. Update database: `dotnet ef database update`

### Adding localization keys
1. Add to `SharedResources.fr.resx` (primary language)
2. Add **real** NL and EN translations to `.nl.resx` and `.en.resx` (the 3 files are at full key parity and fully translated — keep it that way; no `[NL]`/`[EN]` placeholders)
3. Use in views: `@Localizer["Key"]`
4. For JavaScript: Use `@Json.Serialize(Localizer["Key"].Value)` (NOT `@Html.Raw()`)

### Creating a new feature
1. Create folder under `Features/`
2. Add controller, views, ViewModels in feature folder
3. Use existing patterns (repository, services, localization)
4. Add to navigation menu if needed
5. Include tests in seeder if applicable

---

## UI Design Standards

### Colors
- **Primary:** `#007faf` (buttons, links, accents) — hover: `#005f80`
- Functional: `success` green, `danger` red, `warning` yellow, `info` light blue, `secondary` grey

### Typography
- **Index pages:** `<h2><i class="fas fa-icon me-2"></i>@Localizer["Title"]</h2>`
- **Details pages:** `<h1 class="h3 mb-0">@Model.Name</h1>`
- **Form sections:** `<h6 class="mb-3 text-primary"><i class="fas fa-icon me-2"></i>@Localizer["Section"]</h6>` (add `mt-4` for sections after first)
- **Card headers:** `<h6 class="mb-0"><i class="fas fa-icon me-2"></i>Title</h6>`

### Buttons
- **Primary actions (Save/Create):** `btn btn-primary`
- **Edit:** `btn btn-outline-secondary` (tables: add `btn-sm`)
- **Delete:** `btn btn-outline-danger` (tables: `btn-sm`), `btn btn-danger` (confirm page)
- **Back/Cancel:** `btn btn-outline-secondary` — **never** `btn-secondary` (filled)
- **Never use** `btn-warning` for Edit buttons

### Tables
- Always: `<thead class="table-light">`, `<th scope="col">` on all headers
- Classes: `table table-hover`, optionally `table-sm`, `table-responsive`

### Card Headers
- Data tables: `card-header bg-primary text-white`
- Delete confirm: `card-header bg-danger text-white`
- Forms: `card-header` (default grey)
- Info/help: `card-header bg-success text-white` or `bg-info text-white`

### Badges (color = semantic)
- `bg-success`: Confirmed / Active / Paid
- `bg-warning text-dark`: Pending / Unconfirmed
- `bg-danger`: Cancelled / Errors / Locked accounts
- `bg-primary`: Generic counters / IDs
- `bg-secondary`: Inactive / Neutral
- `bg-info`: Enum types (excursion type, payment method)

### Empty States
```html
<div class="text-center py-5">
    <i class="fas fa-icon fa-3x text-muted mb-3"></i>
    <p class="text-muted">@Localizer["NoItemsFound"]</p>
    <a asp-action="Create" class="btn btn-primary">...</a>
</div>
```
**Never** use `<div class="alert alert-info">` for empty states.

---

## Troubleshooting

### Build warnings CS8604/CS8602 (null reference)
- Check for nullable reference types (`string?`)
- Add null checks before calling methods: `!string.IsNullOrEmpty(variable) && variable.StartsWith(...)`
- Use null-forgiving operator `!` only when certain value is not null: `model.Property!`

### Multi-tenancy issues
- Ensure entities have `OrganisationId` and inherit query filter
- Admin actions: Use `.IgnoreQueryFilters()` when needed
- Seeder: Always use `.IgnoreQueryFilters()` for setup data

### Localization not working
- Verify key exists in SharedResources.fr.resx
- Don't set ResourcesPath in Program.cs (known bug)
- Use `.ToString()` when storing LocalizedString in TempData

### File upload issues
- Use IHttpClientFactory for HttpClient
- Validate file paths for traversal attacks
- Check file size limits in attributes
- Use LocalFileStorageService (dev) or AzureBlobStorageService (prod)
