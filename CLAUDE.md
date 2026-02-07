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

**Cedeva** — ASP.NET Core MVC (.NET 9) for managing children's vacation activity centers in Belgium. Multi-tenant SaaS (organisations scope all data). Full spec: [README.md](README.md).

**Stack:** .NET 9 · SQL Server 2022 · EF Core 9 · ASP.NET Identity · Bootstrap 5 · Docker · Brevo email · ClosedXML · Azure Blob Storage

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
│   └── Localization/      # SharedResources.{fr,nl,en}.resx (~600 keys)
├── Cedeva.Core/           # Domain (entities, interfaces, enums)
└── Cedeva.Infrastructure/ # Data (DbContext, migrations, seeder), services
```

### Patterns
- **Multi-tenancy:** EF Core global query filters on `OrganisationId`. Admin bypasses via `.IgnoreQueryFilters()`.
- **Feature folders:** `/Features/{Feature}/{View}.cshtml`
- **Repository + UoW:** `IRepository<T>` with `IUnitOfWork`
- **Localisation:** `@Localizer["Key"]` everywhere. Cookie-based FR/NL/EN. **Do NOT set ResourcesPath** (ASP.NET bug).
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
- **Localisation:** FR (complete), NL/EN (placeholders with `[NL]`/`[EN]` prefixes)
- **Multi-tenancy:** Organisation-scoped data with admin bypass
- **Audit trail:** CreatedAt/CreatedBy/ModifiedAt/ModifiedBy on all 24 entities (auto-populated)

### Financial Module
- Payments, CODA import, bank reconciliation
- Expenses tracking, team salary calculations
- Excel export, color-coded transactions
- `FinancialCalculationService` for reusable business logic

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
- National register number formatting (YY.MM.DD-XXX.XX)
- Email recipient grouping with separators
- Color pickers for iframe customization
- Clean URLs (route parameters vs query strings)

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
2. Add placeholders to `.nl.resx` and `.en.resx` with `[NL]`/`[EN]` prefix
3. Use in views: `@Localizer["Key"]`
4. For JavaScript: Use `@Json.Serialize(Localizer["Key"].Value)` (NOT `@Html.Raw()`)

### Creating a new feature
1. Create folder under `Features/`
2. Add controller, views, ViewModels in feature folder
3. Use existing patterns (repository, services, localization)
4. Add to navigation menu if needed
5. Include tests in seeder if applicable

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
