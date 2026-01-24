# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Cedeva** is an ASP.NET Core MVC application for managing children's vacation activity centers in Belgium. It's a multi-tenant SaaS where organizations manage their activities, child registrations, team members, and parent communications.

## Quick Reference

### Build & Run
```bash
# Restore and build
dotnet restore
dotnet build

# Run with hot reload
dotnet watch run --project src/Cedeva.Website

# Run tests
dotnet test

# Docker
docker-compose up -d
```

### Database
```bash
# Add migration
dotnet ef migrations add <MigrationName> --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website

# Update database
dotnet ef database update --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website
```

## Architecture

### Project Structure
```
src/
├── Cedeva.Website/        # ASP.NET Core MVC (presentation layer)
│   ├── Features/          # Feature-based organization
│   │   ├── Activities/    # Controllers, Views, ViewModels
│   │   ├── Bookings/
│   │   ├── Children/
│   │   ├── Parents/
│   │   ├── TeamMembers/
│   │   ├── Organisations/
│   │   ├── Users/
│   │   └── Shared/        # Layouts, partials, shared components
│   └── Localization/      # FR/NL/EN resources
├── Cedeva.Core/           # Domain layer (entities, interfaces, DTOs)
│   ├── Entities/
│   ├── Enums/
│   ├── Interfaces/
│   └── DTOs/
└── Cedeva.Infrastructure/ # Infrastructure layer
    ├── Data/              # DbContext, configurations
    ├── Services/          # Business services
    ├── Email/             # Brevo integration
    └── Storage/           # Azure Blob Storage
```

### Key Patterns
- **Multi-tenancy**: All queries filtered by `OrganisationId` via EF Core global query filters
- **Feature-based folders**: Each feature is self-contained with its own Controllers/Views/ViewModels
- **View location**: `/Features/{Controller}/{View}.cshtml`

### Authentication & Authorization
- ASP.NET Core Identity with custom `CedevaUser`
- Roles: `Admin` (full access), `Coordinator` (organisation-scoped)
- Admin sees all organisations, Coordinator sees only their own

## Current Progress

### Phase 1: Project Setup (COMPLETED)
- [x] Create solution structure
- [x] Setup Cedeva.Core (entities, enums, interfaces)
- [x] Setup Cedeva.Infrastructure (DbContext, configurations)
- [x] Setup Cedeva.Website (MVC, Identity, Autofac, Serilog)
- [x] Configure Docker support
- [x] Initial migration (InitialCreate)

### Phase 2: Core Features ✅ COMPLETED
- [x] Data seeder (admin user + Belgian municipalities)
- [x] Activity CRUD (full implementation with search, pagination)
- [x] Parent CRUD (full implementation with address management)
- [x] Child CRUD (full implementation with parent linking)
- [x] Organisation CRUD (Admin only - with role-based authorization)
- [x] TeamMember CRUD (full implementation with enums, address, license)
- [x] Booking system (full implementation with child/activity/group relationships)

### Phase 3: Advanced Features (IN PROGRESS)
- [x] Navigation menu and dashboard (completed 2026-01-24)
- [x] User authentication system (login, register, profile - completed 2026-01-24)
- [ ] Iframe registration form
- [ ] Presence management
- [ ] Email sending (Brevo)
- [ ] Excel export (ClosedXML)
- [ ] Localization (FR/NL/EN)

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| Single DB with OrganisationId filter | Simpler than schema/DB per tenant, sufficient for expected scale |
| Azure Blob Storage | Scalable file storage for logos and documents |
| Autofac DI | More powerful than built-in DI, supports modules |
| Feature-based folders | Better organization than traditional MVC folders |
| Bootstrap 5 | Modern, responsive, good component library |

## Important Files

- `cedeva.md` - Full project specification with entity definitions
- `appsettings.json` - Configuration (connection strings, Brevo API, Azure Storage)
- `docker-compose.yml` - Local development with SQL Server

## Notes & Observations

### Setup Notes (2026-01-24)
- Project uses .NET 9.0 with EF Core 9.0.1, Autofac 8.1.0, Serilog 8.0.3
- All NuGet packages pinned to versions compatible with .NET 9
- Docker Compose configured with SQL Server 2022 and health checks
- Initial admin user needs to be created via data seed or manual SQL

### Implementation Notes (2026-01-24 - Session 2)
- Successfully implemented Parent CRUD with:
  - Full address management (Street, City, PostalCode, Country)
  - Belgian national register number validation
  - Child relationship display in Details view
- Successfully implemented Child CRUD with:
  - Parent relationship (dropdown selection)
  - Birth date tracking (using `BirthDate` property from entity)
  - Special needs flags (IsDisadvantagedEnvironment, IsMildDisability, IsSevereDisability)
  - Booking relationship display in Details view
- Successfully implemented Organisation CRUD with:
  - Admin-only access using `[Authorize(Roles = "Admin")]`
  - Full address management (separate Address entity)
  - Logo URL support
  - Statistics dashboard (activities, parents, team members, users counts)
  - Cascade delete warning when organisation has related entities
- Successfully implemented TeamMember CRUD with:
  - Full address management (separate Address entity)
  - Enum dropdowns (TeamRole: Animator/Coordinator, License: License/Assimilated/Internship/Training/NoLicense, Status: Compensated/Volunteer)
  - License URL support for document storage
  - Optional daily compensation field (nullable decimal)
  - Belgian national register number validation
  - Statistics (activities count, expenses count)
  - Age calculation in Details view
- Successfully implemented Booking system CRUD with:
  - Child/Activity/ActivityGroup relationship management (three dropdowns)
  - IsConfirmed boolean for confirmation status tracking
  - IsMedicalSheet boolean for medical sheet tracking
  - Quick confirmation form in Details sidebar
  - Filtering by activity, child, and confirmation status
  - Medical sheet reminder warnings
  - Statistics (booking days count, question answers count)
  - Cascade delete warnings for related Days and QuestionAnswers
- Key learnings:
  - Child entity uses `BirthDate` not `DateOfBirth`
  - Child entity removed `Remarks` property in favor of medical/disability flags
  - Booking entity uses `IsConfirmed` boolean instead of Status enum
  - IRepository interface methods: GetAllAsync(), AddAsync(), UpdateAsync(), DeleteAsync()
  - Organisation requires Address entity to be created first
  - TeamMember uses `TeamMemberId` as primary key (not `Id`)
  - Enums can be used in dropdown with `Html.GetEnumSelectList<EnumType>()`

### Infrastructure Improvements (2026-01-24 - Session 3)
- Successfully implemented navigation menu and dashboard:
  - Updated _Layout.cshtml with sidebar navigation
  - Role-based menu items (Admin section for Organisations and Users)
  - Responsive sidebar with toggle button for mobile
  - Created _ValidationScriptsPartial.cshtml with jQuery validation scripts
  - Enhanced site.css with improved styling for cards, tables, alerts, and forms
  - Created HomeController with dashboard statistics
  - Implemented DashboardViewModel with real-time stats:
    - Activities count (total and active)
    - Bookings count (total and confirmed)
    - Children/Parents count
    - Team members count
    - Recent activities list (top 5)
    - Recent bookings list (top 5)
    - Quick action buttons for common tasks
  - Fixed entity property references (Activity uses `Id`, Booking uses `BookingDate` not `CreatedAt`)

### User Authentication System (2026-01-24 - Session 3)
- Successfully implemented user authentication with ASP.NET Core Identity:
  - AccountController with Login, Register, Logout, Profile actions
  - Login view with remember me functionality and lockout protection
  - Register view with organisation dropdown selection
  - Profile view for users to update FirstName and LastName
  - Added FirstName and LastName properties to CedevaUser entity
  - Organisation dropdown populated from database
  - Auto-assignment of "Coordinator" role on registration
  - Password sign-in with email as username
  - Proper ModelState validation and error handling
  - Beautiful gradient login/register pages (standalone layout)
  - Profile page integrated with main layout
- Key authentication features:
  - Email-based authentication (UserName = Email)
  - Account lockout protection after failed login attempts
  - Remember me functionality
  - Return URL support for redirecting after login
  - Anti-forgery token validation on all POST requests
  - Organisation selection required during registration

### Next Steps
**🎉 MILESTONE: All Phase 2 core CRUD features completed!**

Phase 3 priorities:
1. Create navigation menu and layout for accessing all features
2. Create _ValidationScriptsPartial view (referenced but missing)
3. Implement User management (register, login, roles, profile)
4. Add Excel export functionality (ClosedXML) for activities and bookings
5. Set up localization resource files for FR/NL/EN
6. Create iframe registration form for public bookings
7. Add file upload for logos and license documents (Azure Blob Storage)
8. Implement presence management for daily check-in/check-out
9. Add email sending (Brevo integration) for booking confirmations

### Database Migrations
- **AddUserNameFields** (20260124110514) - Adds FirstName and LastName columns to AspNetUsers table
  - Migration created and ready to apply
  - To apply: `dotnet ef database update --project src/Cedeva.Infrastructure --startup-project src/Cedeva.Website`
  - Requires SQL Server running (via Docker: `docker-compose up -d`)

### Database Seeding
The DbSeeder automatically runs on application startup and provides:
- **Roles**: Admin and Coordinator roles
- **Admin User**: admin@cedeva.be / Admin@123456 (with Admin role)
- **Demo Organisation**: "Plaine de Bossière" in Gembloux
- **Demo Coordinator**: coordinator@cedeva.be / Coord@123456 (linked to demo organisation)
- **Belgian Municipalities**: 150+ Belgian postal codes and cities for address autocomplete

All seeded users have:
- FirstName and LastName fields populated
- EmailConfirmed set to true
- Proper role assignments

**First Run Instructions:**
1. Start SQL Server: `docker-compose up -d`
2. Run the application: `dotnet run --project src/Cedeva.Website`
3. Seeder will automatically create database, apply migrations, and seed data
4. Login with admin@cedeva.be or coordinator@cedeva.be

### Known Issues
- 16 nullable conversion warnings in Index.cshtml files (ViewData casts) - Children, TeamMembers, Organisations, Bookings
- Activity and Booking entities don't have CreatedAt/UpdatedAt audit fields
- Database migration requires SQL Server running to apply
- No password reset functionality implemented
- No email confirmation for new user registrations
- No admin Users CRUD interface yet for managing user accounts

---
Last updated: 2026-01-24 (Phase 3 in progress - Authentication completed)
