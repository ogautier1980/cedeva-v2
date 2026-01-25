# CLAUDE.md

Guide for Claude Code when working with the Cedeva codebase.

## ⚠️ Working Principles - READ THIS FIRST

**CRITICAL**: Apply absolute rigor and thoroughness to ALL work on this project.

### Golden Rules
1. **Never make claims without verification**
   - ❌ Never say "100% complete", "fully done", "all X are Y" without actual proof
   - ✅ Say "I've completed X, Y, Z" with specific file names and line numbers
   - ✅ If unsure, say "I believe X is done, but let me verify" then actually verify

2. **Verify before stating**
   - Read files before claiming they're modified
   - Run searches before claiming nothing remains
   - Test changes before claiming they work
   - Double-check before pushing code

3. **Be specific and factual**
   - Bad: "All views are localized"
   - Good: "I localized 24 files: Account/Login.cshtml (lines 10, 25), Activities/Create.cshtml (lines 15, 30)..."

4. **When asked to verify**
   - Actually read files line by line if needed
   - Don't rely only on grep - patterns can miss things
   - If you find errors, fix them immediately
   - Never say "looks good" without checking

5. **Document what you actually did**
   - Commit messages must list specific changes
   - Update this file with factual information only
   - Remove old/incorrect information

**This applies to localization, bug fixes, features, refactoring, documentation - EVERYTHING.**

---

## Project Overview

**Cedeva** is an ASP.NET Core MVC application for managing children's vacation activity centers in Belgium. Multi-tenant SaaS where organizations manage activities, child registrations, team members, and parent communications.

**Stack**: .NET 9 • SQL Server • Entity Framework Core • ASP.NET Identity • Bootstrap 5 • Docker

**Reference**: See [cedeva.md](cedeva.md) for complete technical specification

## Quick Reference

### Commands
```bash
# Build & Run
dotnet build
dotnet watch run --project src/Cedeva.Website

# Database
dotnet ef migrations add <Name> --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website

# Docker
docker-compose up -d
```

### Login Credentials (Seeded)
- **Admin**: admin@cedeva.be / Admin@123456
- **Coordinator (Org 1)**: coordinator@cedeva.be / Coord@123456
- **Coordinator (Org 2)**: coordinator.liege@cedeva.be / Coord@123456

### Key Files
- **cedeva.md** - Complete technical specification with entity schemas
- **appsettings.json** - Connection strings, Brevo API, Azure Storage keys
- **docker-compose.yml** - SQL Server 2022 configuration
- **Program.cs** - Service registration, middleware pipeline

## Architecture

### Project Structure
```
src/
├── Cedeva.Website/        # MVC Presentation (feature-based folders)
│   ├── Features/          # Activities, Bookings, Children, Parents, TeamMembers, etc.
│   └── Localization/      # FR/NL/EN resource files
├── Cedeva.Core/           # Domain (entities, interfaces, enums)
└── Cedeva.Infrastructure/ # Data access, services (email, storage, Excel)
```

### Key Patterns
- **Multi-tenancy**: OrganisationId filtering via EF Core global query filters
- **Feature folders**: `/Features/{Controller}/{View}.cshtml`
- **DI**: Autofac for service registration
- **Auth**: ASP.NET Identity with Admin/Coordinator roles
- **Repository Pattern**: `IRepository<T>` with Unit of Work
- **Localization**: `IStringLocalizer<SharedResources>` throughout

### Technology Stack
| Component | Technology |
|-----------|-----------|
| Backend | ASP.NET Core MVC (.NET 9) |
| Database | SQL Server 2022 |
| ORM | Entity Framework Core 9 |
| Email | Brevo API (sib_api_v3_sdk) |
| Excel | ClosedXML |
| Storage | Azure Blob Storage |
| Localization | FR/NL/EN (resource files) |
| Frontend | Bootstrap 5 + jQuery + FontAwesome |

### Key Entities
- **Organisation** - Multi-tenant root entity (Name, Address, LogoUrl)
- **Activity** - Stage/vacation program (Name, StartDate, EndDate, PricePerDay, IsActive)
- **Booking** - Child registration (Child, Activity, Group, IsConfirmed, IsMedicalSheet)
- **Child** - Registered child (FirstName, LastName, **BirthDate**, Parent, disability flags)
- **Parent** - Guardian contact (Name, Email, Phone, Address, NationalRegisterNumber)
- **TeamMember** - Staff member (Name, TeamRole, License, Status, DailyCompensation)
  - Uses `TeamMemberId` as primary key (not `Id`)
- **CedevaUser** - Identity user (FirstName, LastName, OrganisationId, Role)

## Implementation Status

### ✅ Phase 1-4: Core Features (COMPLETED)
- [x] Project setup with Docker, SQL Server 2022
- [x] Activities, Bookings, Children, Parents, TeamMembers CRUD
- [x] Dashboard with role-based navigation
- [x] Authentication & Authorization (ASP.NET Identity)
- [x] Excel Export (ClosedXML)
- [x] Email Service (Brevo API)
- [x] Public Registration Form (multi-step wizard, embeddable iframe)
- [x] Presence Management (daily attendance tracking)
- [x] ActivityManagement Module (centralized activity hub)

### 🔄 Phase 5: Localization (IN PROGRESS)
- [x] Localization infrastructure (FR/NL/EN resource files)
- [x] Language switcher in navbar
- [x] Most views localized with @Localizer pattern
- [x] ViewModel validation messages localized
- [x] Controller TempData messages localized
- [ ] **VERIFICATION NEEDED**: User reported French text still exists in views
- [ ] Full audit of all .cshtml files for hardcoded French text
- [ ] Translation of NL and EN resource values (currently marked with [NL]/[EN] prefixes)

## Key Features

### Multi-Tenancy
All entities filtered by `OrganisationId` via EF Core global query filters:
```csharp
modelBuilder.Entity<Activity>().HasQueryFilter(a =>
    a.OrganisationId == _currentUserService.GetOrganisationId());
```
Admin users bypass filters. Seeding requires `.IgnoreQueryFilters()`.

### Database Seeding
Auto-seeding on startup includes:
- Admin and Coordinator roles
- Demo users (admin@cedeva.be, coordinator@cedeva.be, coordinator.liege@cedeva.be)
- Two demo organisations (Gembloux, Liège)
- 150+ Belgian municipalities for address autocomplete
- Test data: 25 parents, 12 team members, 4 activities per organisation (Belgian names, valid national register numbers)

### Email Service (Brevo)
Professional HTML templates for:
- Booking confirmation (green theme)
- Welcome email (blue theme)
Configuration in `appsettings.json`: Brevo:ApiKey, SenderEmail, SenderName

### Excel Export
Generic service using `Dictionary<string, Func<T, object>>` for column mapping. Features: auto-formatting, timestamped filenames, filter preservation.

### Localization
Cookie-based language preference (FR/NL/EN):
- Resource files: `Localization/SharedResources.{culture}.resx`
- Usage: `@inject IStringLocalizer<SharedResources> Localizer` then `@Localizer["Key"]`
- Validation: DataAnnotationsLocalization configured to use SharedResources
- Switcher: Navbar dropdown with flag emojis

Pattern for validation:
```csharp
// Before (hardcoded)
[Required(ErrorMessage = "Le prénom est requis")]
[Display(Name = "Prénom")]

// After (localized)
[Required]
[Display(Name = "Field.FirstName")]
```

### Public Registration Form
Multi-step wizard at `/PublicRegistration/SelectActivity?orgId={id}`:
1. Activity selection (choose activity and days)
2. Parent information (email, name, phone, address)
3. Child information (birth date, medical info, disabilities)
4. Custom questions (if configured for activity)
5. Confirmation (review and submit)

- State managed via TempData
- Smart duplicate detection by email (parent) and national register number (child)
- Email confirmation via Brevo
- Embeddable iframe code at `/PublicRegistration/EmbedCode?orgId={id}`

### Presence Management
Daily attendance tracking at `/Presence`:
1. **Index** - Card grid of activities
2. **SelectDay** - Choose activity day
3. **List** - Interactive checklist with AJAX updates
4. **Print** - A4 printable format with signature boxes

Uses `BookingDay.IsPresent` boolean. Real-time count updates via JavaScript.

### ActivityManagement Module
Centralized activity management hub accessible from Activities list.

Features:
1. **Dashboard** - 7 action cards (bookings, presences, emails, team, etc.)
2. **UnconfirmedBookings** - Confirm bookings and assign groups
3. **Presences** - Enhanced attendance tracking with group view
4. **SendEmail** - Targeted emails (all parents, by group, medical reminders)
5. **SentEmails** - Email history with detail modal
6. **TeamMembers** - Assign/unassign team members to activity

Session-based activity selection. Direct `DbContext` access.

### Custom Claims Authentication
`CedevaUserClaimsPrincipalFactory` adds Role and OrganisationId claims:
```csharp
identity.AddClaim(new Claim("Role", user.Role.ToString()));
if (user.OrganisationId.HasValue)
    identity.AddClaim(new Claim("OrganisationId", user.OrganisationId.Value.ToString()));
```
Registered in `Program.cs`: `.AddClaimsPrincipalFactory<CedevaUserClaimsPrincipalFactory>()`

## Development Workflow

### First Run
1. Start SQL Server: `docker-compose up -d`
2. Run app: `dotnet run --project src/Cedeva.Website`
3. Auto-seeding creates DB, applies migrations, seeds test data
4. Login as admin or coordinator (see credentials above)

### Adding Migrations
```bash
dotnet ef migrations add DescriptiveName --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
```

### Repository Pattern
- Interface methods: `GetAllAsync()`, `GetByIdAsync(id)`, `AddAsync(entity)`, `UpdateAsync(entity)`, `DeleteAsync(entity)`
- Unit of Work: `await _unitOfWork.SaveChangesAsync()` after operations

## Localization Status

**Current state** (as of last verified commit):

### Infrastructure
- ✅ Resource files created (SharedResources.fr.resx, .nl.resx, .en.resx)
- ✅ IStringLocalizer injected in controllers
- ✅ DataAnnotationsLocalization configured
- ✅ Language switcher in navbar

### What's Localized
Recent work localized:
- 24 view files (Account, Activities, Bookings, Home, Organisations, PublicRegistration, Users)
- Button labels (Save, Cancel, Back, Edit, Delete)
- Dropdown placeholders
- Role enum values
- Help text and lead paragraphs
- Warning and info messages

13 resource keys added in last session:
- BookedDays, QuestionAnswers, Account.CreateAccount
- Edit, Delete, Save, Cancel, Back
- Organisations.ViewActivities, Organisations.DeleteCascadeWarning
- PublicRegistration.FinalizeBooking, Users.CreateUser
- AllOrganisations, Enum.Role.Coordinator, Enum.Role.Admin

### What Still Needs Work
- ⚠️ **User reported French text still exists** - full audit required
- 🔄 Translate ~360 NL resource values (marked with [NL] prefix)
- 🔄 Translate ~360 EN resource values (marked with [EN] prefix)

### Verification Commands
```bash
# Search for French text in views (excluding localized patterns)
cd src/Cedeva.Website/Features
grep -rn --include="*.cshtml" "Aucun\|Veuillez\|Sélectionne\|Modifier\|Supprimer" . | grep -v '@Localizer'

# List all .cshtml files for manual review
find Features -name "*.cshtml" -type f

# Check resource file counts
grep -c '<data name=' Localization/SharedResources.fr.resx
```

## Bug Fixes & Important Notes

### Nullable Parameter Fix
Fixed 400 Bad Request errors by making `searchString` nullable in Index actions:
```csharp
public async Task<IActionResult> Index(string? searchString, int page = 1)
```
Affected: Bookings, Children, TeamMembers, Organisations, Users

### Query Filter Bypass in Seeding
Always use `.IgnoreQueryFilters()` in seeders (no authenticated user context).

### Child Entity Property Name
**IMPORTANT**: Child uses `BirthDate` property (NOT `DateOfBirth`)

### Booking Status
**IMPORTANT**: Booking uses `IsConfirmed` boolean (NOT a Status enum)

## Known Issues & Future Work

### Current Warnings
- 17 nullable conversion warnings in Index.cshtml files (ViewData casts)
- Non-critical, can be ignored

### Future Enhancements
- Password reset functionality
- Email confirmation for new registrations
- Audit fields (CreatedAt/UpdatedAt) on entities
- Reports and analytics dashboard
- Payment integration (Mollie/Stripe)
- Advanced search and filtering
- Mobile app for team members (presence tracking)
- PWA support

---

**Last updated**: 2026-01-25
**Current phase**: Phase 5 (Localization - IN PROGRESS, verification needed)
