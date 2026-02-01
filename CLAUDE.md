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

### 🔄 In Progress
- Phase 7: UX improvements (postal code autocomplete, booking day cards, admin org selection)
- NL/EN translation completion

---

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