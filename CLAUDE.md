# CLAUDE.md

Guide for Claude Code when working with the Cedeva codebase.

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
- **Coordinator**: coordinator@cedeva.be / Coord@123456

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

## Implementation Status

### ✅ Phase 1: Project Setup (COMPLETED)
- Solution structure (Core, Infrastructure, Website)
- Docker support with SQL Server 2022
- Initial migration and database setup

### ✅ Phase 2: Core CRUD Features (COMPLETED)
- [x] Activities - Full CRUD with search, pagination, active/inactive filtering
- [x] Bookings - Registration management with child/activity/group relationships
- [x] Children - CRUD with parent linking, disability flags
- [x] Parents - CRUD with address management, national register number
- [x] TeamMembers - CRUD with roles, licenses, compensation tracking
- [x] Organisations - Admin-only CRUD with address, logo

### 🚧 Phase 3: Advanced Features (IN PROGRESS - 6/7 Complete)
- [x] **Dashboard & Navigation** - Sidebar menu, statistics, role-based access
- [x] **Authentication** - Login, register, profile, lockout protection
- [x] **Excel Export** - ClosedXML integration for all major entities
- [x] **Email Sending** - Brevo integration with booking confirmation & welcome emails
- [x] **Localization** - FR/NL/EN support with language switcher
- [x] **Iframe Registration Form** - Public booking form with multi-step wizard, embeddable code
- [ ] **Presence Management** - Daily attendance tracking with list printing

## Key Technical Details

### Database Seeding
Auto-seeding on startup includes:
- Admin and Coordinator roles
- Demo users (admin@cedeva.be, coordinator@cedeva.be)
- Demo organisation "Plaine de Bossière"
- 150+ Belgian municipalities for address autocomplete

### Multi-tenancy Implementation
All entities filtered by `OrganisationId` via EF Core:
```csharp
// Global query filter in DbContext
modelBuilder.Entity<Activity>().HasQueryFilter(a =>
    a.OrganisationId == _currentUserService.GetOrganisationId());
```
Admin users bypass filters to see all organisations.

### Email Service
Brevo integration with professional HTML templates:
- **Booking confirmation** - Green theme, sent when booking confirmed
- **Welcome email** - Blue theme, sent on user registration
- Configuration: `appsettings.json` → Brevo:ApiKey, SenderEmail, SenderName

### Excel Export
Generic service using `Dictionary<string, Func<T, object>>` for column mapping:
```csharp
var columns = new Dictionary<string, Func<Booking, object>> {
    { "Enfant", b => $"{b.Child.FirstName} {b.Child.LastName}" },
    { "Activité", b => b.Activity.Name },
    // ...
};
var excelData = _excelExportService.ExportToExcel(bookings, "Inscriptions", columns);
```
Features: Auto-formatting, timestamped filenames, filter preservation

### Localization
Cookie-based language preference (FR/NL/EN):
- Resource files: `Localization/SharedResources.{culture}.resx`
- Switcher: Navbar dropdown with flag emojis
- Default: French (fr)
- Controller action: `HomeController.SetLanguage(culture, returnUrl)`

### Iframe Registration Form
Public booking form accessible without authentication at `/PublicRegistration/SelectActivity?orgId={id}`:
- **Multi-step wizard**: Activity selection → Parent info → Child info → Custom questions → Confirmation
- **Smart parent/child detection**: Checks email/national register number to update existing records
- **Custom questions**: Supports Text, Checkbox, Radio, Dropdown types via ActivityQuestion entity
- **Email confirmation**: Sends booking details to parent email via Brevo
- **Embeddable code**: Generate iframe HTML at `/PublicRegistration/EmbedCode?orgId={id}` (Admin/Coordinator only)
- **Gradient UI**: Beautiful standalone public layout with purple gradient background
- **TempData state**: Maintains wizard state across steps
```csharp
// Example: Get embed code for organisation 1
// Navigate to: /PublicRegistration/EmbedCode?orgId=1
// Copy generated iframe code to embed on external website
```

## Entity Quick Reference

### Key Entities
- **Organisation** - Multi-tenant root entity (Name, Address, LogoUrl)
- **Activity** - Stage/vacation program (Name, StartDate, EndDate, PricePerDay, IsActive)
- **Booking** - Child registration (Child, Activity, Group, IsConfirmed, IsMedicalSheet)
- **Child** - Registered child (FirstName, LastName, BirthDate, Parent, disability flags)
- **Parent** - Guardian contact (Name, Email, Phone, Address, NationalRegisterNumber)
- **TeamMember** - Staff member (Name, TeamRole, License, Status, DailyCompensation)
- **CedevaUser** - Identity user (FirstName, LastName, OrganisationId, Role)

### Important Notes
- **Child** uses `BirthDate` property (not `DateOfBirth`)
- **Booking** uses `IsConfirmed` boolean (no Status enum)
- **TeamMember** uses `TeamMemberId` as primary key (not `Id`)
- **Address** is separate entity shared by Parent, TeamMember, Organisation

## Development Workflow

### First Run
1. Start SQL Server: `docker-compose up -d`
2. Run app: `dotnet run --project src/Cedeva.Website`
3. Auto-seeding creates DB, applies migrations, seeds data
4. Login: admin@cedeva.be / Admin@123456

### Adding Migrations
```bash
# After modifying entities
dotnet ef migrations add DescriptiveName --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
dotnet ef database update --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
```

### Repository Pattern
Interface methods: `GetAllAsync()`, `GetByIdAsync(id)`, `AddAsync(entity)`, `UpdateAsync(entity)`, `DeleteAsync(entity)`

Unit of Work: `await _unitOfWork.SaveChangesAsync()` after repository operations

### Enum Dropdowns
```csharp
ViewBag.Roles = Html.GetEnumSelectList<TeamRole>();
```

## Configuration Files

- **cedeva.md** - Complete technical specification with entity schemas
- **appsettings.json** - Connection strings, Brevo API, Azure Storage keys
- **docker-compose.yml** - SQL Server 2022 configuration
- **Program.cs** - Service registration, middleware pipeline

## Known Issues & TODOs

### Current Warnings
- 17 nullable conversion warnings in Index.cshtml files (ViewData casts)
- Can be safely ignored - these are non-critical nullable reference warnings

### Missing Features
- Password reset functionality
- Email confirmation for new registrations
- Admin Users CRUD interface
- Audit fields (CreatedAt/UpdatedAt) on Activity and Booking entities

### Phase 3 Remaining
1. **Iframe registration form** - Embeddable public form for parents
2. **Presence management** - Daily check-in/check-out with list printing

---

**Last updated**: 2026-01-24
**Current phase**: Phase 3 (5/7 features complete)
**Next priority**: Iframe registration form or Presence management
