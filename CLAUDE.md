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
- **Coordinator (Org 1)**: coordinator@cedeva.be / Coord@123456
- **Coordinator (Org 2)**: coordinator.liege@cedeva.be / Coord@123456

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

### ✅ Phase 3: Advanced Features (COMPLETED)
- [x] **Dashboard & Navigation** - Sidebar menu, statistics, role-based access
- [x] **Authentication** - Login, register, profile, lockout protection
- [x] **Excel Export** - ClosedXML integration for all major entities
- [x] **Email Sending** - Brevo integration with booking confirmation & welcome emails
- [x] **Localization** - FR/NL/EN support with language switcher
- [x] **Iframe Registration Form** - Public booking form with multi-step wizard, embeddable code
- [x] **Presence Management** - Daily attendance tracking with list printing

### ✅ Phase 4: ActivityManagement Module (COMPLETED)
- [x] **Dashboard** - Centralized activity management hub with action cards
- [x] **UnconfirmedBookings** - Confirm bookings and assign groups
- [x] **Presences** - Daily attendance tracking with AJAX updates
- [x] **SendEmail** - Targeted email sending (all parents, by group, medical sheet reminders)
- [x] **SentEmails** - Email history with detailed view
- [x] **TeamMembers** - Assign/unassign team members to activity

## Key Technical Details

### Database Seeding
Auto-seeding on startup includes:
- Admin and Coordinator roles
- Demo users (admin@cedeva.be, coordinator@cedeva.be, coordinator.liege@cedeva.be)
- Two demo organisations:
  - **Plaine de Bossière** (Gembloux) - Org 1
  - **Centre Récréatif Les Aventuriers** (Liège) - Org 2
- 150+ Belgian municipalities for address autocomplete
- Realistic test data for each organisation (via TestDataSeeder):
  - 25 parents with 1-3 children each (Belgian names, valid national register numbers)
  - 12 team members with valid phone numbers
  - 4 activities with days and groups
  - Bookings for 60% of children

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

### Presence Management
Daily attendance tracking feature at `/Presence`:
- **Activity selection**: Card-based view of all active activities
- **Day selection**: Choose specific activity day for attendance tracking
- **Interactive list**: Real-time present count, check all/uncheck all functionality
- **Printable format**: A4 print view with signature boxes for coordinators
- **Workflow**: Index → SelectDay → List (with AJAX updates) → Print view
```csharp
// Presence tracking accessible at:
// 1. /Presence - Select activity
// 2. /Presence/SelectDay?activityId={id} - Select day
// 3. /Presence/List?activityId={id}&dayId={dayId} - Mark attendance
// 4. /Presence/Print?activityId={id}&dayId={dayId} - Print list
```

### Custom Claims Authentication
Custom `CedevaUserClaimsPrincipalFactory` adds Role and OrganisationId claims:
```csharp
public class CedevaUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<CedevaUser, IdentityRole>
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(CedevaUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("Role", user.Role.ToString()));
        if (user.OrganisationId.HasValue)
            identity.AddClaim(new Claim("OrganisationId", user.OrganisationId.Value.ToString()));
        return identity;
    }
}
```
Registered in `Program.cs`: `.AddClaimsPrincipalFactory<CedevaUserClaimsPrincipalFactory>()`

### Test Data Seeder
Automatic seeding of realistic Belgian test data for all organisations:
- **Belgian names**: Mix of French (Antoine, Emma) and Dutch (Lucas, Mila) names
- **Valid national register numbers**: Format YY.MM.DD-XXX.YY with proper checksum
- **Valid phone numbers**: Mobile (04XX XX XX XX) and landline (0X XXX XX XX)
- **Intelligent seeding**: Only seeds if organisation has <10 parents/team members
- **Multi-org support**: Loops through all organisations and seeds independently
```csharp
// Seeded for each organisation:
// - 25 parents with 1-3 children each
// - 12 team members (mix of roles and licenses)
// - 4 activities with days and groups
// - Bookings for ~60% of children with booking days
```

## Bug Fixes & Improvements

### Nullable Parameter Fix (Phase 3)
Fixed 400 Bad Request errors in all index pages by making `searchString` parameter nullable:
```csharp
// Before: public async Task<IActionResult> Index(string searchString, int page = 1)
// After:  public async Task<IActionResult> Index(string? searchString, int page = 1)
```
Affected controllers: Bookings, Children, TeamMembers, Organisations, Users

### Query Filter Bypass in Seeding
Added `.IgnoreQueryFilters()` to database queries in `TestDataSeeder` and `DbSeeder`:
```csharp
// Required because seeding runs without authenticated user context
var children = await _context.Children
    .IgnoreQueryFilters()
    .Include(c => c.Parent)
    .Where(c => c.Parent.OrganisationId == organisationId)
    .ToListAsync();
```

### Custom Claims for Authorization
Fixed admin menu visibility by properly inheriting from `UserClaimsPrincipalFactory<CedevaUser, IdentityRole>` instead of `UserClaimsPrincipalFactory<CedevaUser>`. This ensures role claims are properly added alongside custom claims.

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

## Key Feature Details

### Excel Export Implementation
Uses `ClosedXMLExportService` implementing `IExcelExportService`:
```csharp
// Example usage in BookingsController
var columns = new Dictionary<string, Func<Booking, object>>
{
    { "Enfant", b => $"{b.Child.FirstName} {b.Child.LastName}" },
    { "Parent", b => $"{b.Child.Parent.FirstName} {b.Child.Parent.LastName}" },
    { "Email Parent", b => b.Child.Parent.Email },
    { "Activité", b => b.Activity.Name },
    { "Groupe", b => b.Group?.Label ?? "N/A" },
    { "Date d'inscription", b => b.BookingDate.ToString("dd/MM/yyyy") },
    { "Confirmé", b => b.IsConfirmed ? "Oui" : "Non" },
    { "Fiche médicale", b => b.IsMedicalSheet ? "Oui" : "Non" }
};
var excelData = _excelExportService.ExportToExcel(filteredBookings, "Inscriptions", columns);
return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    $"Inscriptions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
```

### Presence Management Workflow
Four-step process for daily attendance tracking:
1. **Index** - Shows card grid of all activities (active only for coordinators)
2. **SelectDay** - Calendar-like view of activity days with date badges
3. **List** - Interactive checklist with real-time counter and AJAX updates
4. **Print** - A4 printable format with coordinator signature boxes

Key implementation details:
- Uses `BookingDay.IsPresent` boolean for attendance status
- Real-time count updates via JavaScript on checkbox change
- Filters by activity and day using composite query
- Supports bulk check/uncheck all functionality

### ActivityManagement Module
Centralized module for managing all aspects of an activity in one place. Accessible from the Activities list.

**Features implemented**:
1. **Dashboard** (`/ActivityManagement/Index`) - Hub with 7 action cards:
   - Inscriptions en attente (Unconfirmed Bookings) ✅
   - Présences (Attendance tracking) ✅
   - Comptes (Accounts - placeholder)
   - E-mails (Targeted email sending) ✅
   - Excursions (placeholder)
   - Équipe (Team management) ✅
   - ONE (Office National de l'Enfance - placeholder)

2. **UnconfirmedBookings** (`/ActivityManagement/UnconfirmedBookings`) - Confirm bookings and assign groups:
   - List all bookings where `IsConfirmed = false`
   - Dropdown to select target group for each child
   - One-click confirmation with group assignment
   - Automatically updates `Booking.GroupId` and `IsConfirmed` flag

3. **Presences** (`/ActivityManagement/Presences`) - Enhanced attendance tracking:
   - Activity day selector (dropdown with dates)
   - Children grouped by `ActivityGroup`
   - Real-time checkbox updates via AJAX endpoint `UpdatePresence`
   - Visual feedback with `table-success` class for present children
   - Displays `IsReserved` status alongside presence checkbox

4. **SendEmail** (`/ActivityManagement/SendEmail`) - Targeted email communications:
   - **Recipient selection**:
     - All parents (children registered to activity)
     - Medical sheet reminder (parents with `IsMedicalSheet = false`)
     - By group (specific ActivityGroup)
   - Subject and message composition
   - Optional file attachment support (saved to `wwwroot/uploads/attachments`)
   - Sends via Brevo API using `IEmailService`
   - Logs to `EmailsSent` table with full details
   - Uses `IEmailRecipientService` for dynamic recipient filtering

5. **SentEmails** (`/ActivityManagement/SentEmails`) - Email history:
   - Displays all emails sent for the activity (ordered by date DESC)
   - Shows recipient type, count, subject, attachment indicator
   - Modal detail view with full message and recipient list
   - Link to send new email

6. **TeamMembers** (`/ActivityManagement/TeamMembers`) - Team assignment:
   - **Two-column layout**:
     - Left: Assigned team members with details (role, license, status)
     - Right: Available team members (not yet assigned)
   - One-click add/remove team member assignments
   - Uses existing many-to-many relationship (`Activity.TeamMembers`)
   - Filters available members by organisation
   - Shows role badges (Coordinator, Animator, Cook, Maintenance)
   - Shows license badges (Breveté, Coordinateur, None)
   - Confirmation prompt before removing team member

**Technical implementation**:
- Session-based activity selection (`SessionActivityId` constant)
- Direct `DbContext` access for queries (no repository pattern in this module)
- Service dependencies: `IEmailService`, `IEmailRecipientService`
- `EmailRecipientService` filters recipients based on criteria:
  - `allparents`: All confirmed bookings for activity
  - `medicalsheetreminder`: Bookings where `IsMedicalSheet = false`
  - `group_X`: Bookings in specific `ActivityGroup`

**Access pattern**:
```
Activities → Select activity → ActivityManagement/Index (session stored)
   → All actions use session to maintain context
   → Can override via query parameter: ?id=X
```

### Public Registration Form Flow
Multi-step wizard with state management via TempData:
```
Step 1: SelectActivity (choose activity and days)
   ↓ TempData["SelectedActivity"]
Step 2: ParentInformation (email, name, phone, address)
   ↓ TempData["ParentInfo"]
Step 3: ChildInformation (child details, medical info)
   ↓ TempData["ChildInfo"]
Step 4: ActivityQuestions (custom questions if configured)
   ↓ TempData["QuestionAnswers"]
Step 5: Confirmation (review and submit)
   ↓ Creates Booking, sends email
```

Smart duplicate detection:
- Parent: Matches by email address (updates if exists)
- Child: Matches by national register number (updates if exists)
- Prevents duplicate bookings for same child/activity combination

### Localization Strategy
Three-language support (FR, NL, EN) using resource files:
- **SharedResources.fr.resx** - French (default)
- **SharedResources.nl.resx** - Dutch
- **SharedResources.en.resx** - English

Usage in views:
```csharp
@using Microsoft.Extensions.Localization
@inject IStringLocalizer<SharedResources> Localizer

<h1>@Localizer["Activities"]</h1>
```

Language selection stored in cookie, persists across sessions. Switcher in navbar with flag emojis.

## Development Workflow

### First Run
1. Start SQL Server: `docker-compose up -d`
2. Run app: `dotnet run --project src/Cedeva.Website`
3. Auto-seeding creates DB, applies migrations, seeds 2 organisations with test data
4. Login options:
   - Admin: admin@cedeva.be / Admin@123456 (sees all organisations)
   - Coordinator Org 1: coordinator@cedeva.be / Coord@123456
   - Coordinator Org 2: coordinator.liege@cedeva.be / Coord@123456

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

### Future Enhancements
- Password reset functionality
- Email confirmation for new registrations
- Audit fields (CreatedAt/UpdatedAt) on Activity and Booking entities
- Reports and analytics dashboard
- Payment integration for activity fees
- Advanced search and filtering across all modules
- Mobile app for team members (presence tracking)

### Phase 4 Candidates
- **Reports Module**: Activity reports, financial reports, attendance statistics
- **Payment Integration**: Mollie/Stripe integration for online payments
- **Advanced Notifications**: SMS notifications, reminder emails
- **Mobile Optimization**: PWA support, mobile-first presence tracking
- **API Development**: RESTful API for third-party integrations

## Localization (FR/NL/EN)

Complete multi-language support implemented across the entire application.

### Status: COMPLETED ✅ (100%)

**⚠️ IMPORTANT - Rigor in Localization:**
Localization requires **absolute rigor and thoroughness**. When localizing:
- ✅ **Read files carefully** - Never claim completion without verifying ALL French text is localized
- ✅ **Check ALL locations** - Views, ViewModels, Controllers, placeholders, help text, buttons, labels
- ✅ **Validate ValidationMessages** - Remove hardcoded ErrorMessage attributes, use resource keys
- ✅ **Test systematically** - Go through each file methodically, don't skip or assume
- ❌ **Never claim "100% ready"** or "fully localized" without thorough verification
- ❌ **Never batch work** - Complete one module fully before moving to the next

**Completed Work:**
- ✅ **All view files localized** (58 views across 13 features)
- ✅ **All controller TempData messages localized** (33 messages across 10 controllers)
- ✅ **All ViewModel validation attributes localized** (14 ViewModels)
- ✅ **Shared layouts and partial views localized**
- ✅ **Public registration form fully localized**
- ✅ **Dashboard and navigation localized**
- ✅ **Resource files synchronized**:
  - SharedResources.fr.resx: 584 keys (French - complete)
  - SharedResources.nl.resx: 572 keys (Dutch - 212 translated, 360 marked with [NL] prefix for translation)
  - SharedResources.en.resx: 572 keys (English - 212 translated, 360 marked with [EN] prefix for translation)

**Views Localized:**
- Activities (5 views) - Create, Edit, Details, Delete, Index
- Bookings (5 views) - Create, Edit, Details, Delete, Index
- Presence (4 views) - Index, SelectDay, List, Print
- ActivityManagement (6 views) - Index, UnconfirmedBookings, Presences, SendEmail, SentEmails, TeamMembers
- Children (5 views) - Create, Edit, Details, Delete, Index
- Parents (5 views) - Create, Edit, Details, Delete, Index
- TeamMembers (5 views) - Create, Edit, Details, Delete, Index
- Organisations (5 views) - Create, Edit, Details, Delete, Index
- Users (5 views) - Create, Edit, Details, Delete, Index
- Account (4 views) - Login, Register, Profile, AccessDenied
- Home (2 views) - Index (Dashboard), Error
- Shared Layouts (3 views) - _Layout, _PublicLayout, Components
- PublicRegistration (6 views) - SelectActivity, ParentInformation, ChildInformation, ActivityQuestions, Confirmation, EmbedCode

**ViewModels with Localized Validations:**
1. ChildViewModel - All Display and ErrorMessage attributes localized
2. ParentViewModel - All Display and ErrorMessage attributes localized
3. ActivityViewModel - All Display and ErrorMessage attributes localized
4. TeamMemberViewModel - All Display and ErrorMessage attributes localized
5. OrganisationViewModel - All Display and ErrorMessage attributes localized
6. UserViewModel - All Display and ErrorMessage attributes localized
7. LoginViewModel - All Display and ErrorMessage attributes localized
8. RegisterViewModel - All Display and ErrorMessage attributes localized
9. ProfileViewModel - All Display and ErrorMessage attributes localized
10. BookingViewModel - All Display and ErrorMessage attributes localized
11. ChildInformationViewModel - All Display and ErrorMessage attributes localized
12. ParentInformationViewModel - All Display and ErrorMessage attributes localized
13. SelectActivityViewModel - All Display and ErrorMessage attributes localized
14. SendEmailViewModel - All Display and ErrorMessage attributes localized

**Controllers with Localized TempData:**
- ActivitiesController, BookingsController, ChildrenController, ParentsController
- TeamMembersController, OrganisationsController, UsersController, AccountController
- PresenceController, ActivityManagementController

**Resource Keys Categories (584 total in FR):**
- Field.* (90+ keys) - Display names for form fields
- Validation.* (20+ keys) - Validation error messages
- Activities.* (40+ keys) - Activities feature
- Bookings.* (35+ keys) - Bookings feature
- Children.* (25+ keys) - Children feature
- Parents.* (25+ keys) - Parents feature
- TeamMembers.* (30+ keys) - Team members feature
- Account.* (20+ keys) - Account & Authentication
- PublicRegistration.* (40+ keys) - Public registration form
- Home.* (25+ keys) - Dashboard and navigation
- Presence.* (20+ keys) - Presence tracking
- ActivityManagement.* (30+ keys) - Activity management module
- Shared UI (50+ keys) - Buttons, labels, common text
- Enums (80+ keys) - Localized enum values

**Remaining Work for Full Multi-Language Support:**
- 🔄 Translate 360 keys in SharedResources.nl.resx (marked with [NL] prefix)
- 🔄 Translate 360 keys in SharedResources.en.resx (marked with [EN] prefix)
- Note: All infrastructure is in place, only translations needed

**Technical Implementation:**
- Uses `IStringLocalizer<SharedResources>` pattern
- DataAnnotationsLocalization configured in Program.cs to use SharedResources for all validation messages
- Cookie-based language preference
- Language switcher in navbar with flag emojis
- Format strings support with placeholders: `string.Format(_localizer["Key"].Value, param)`
- All controllers have IStringLocalizer injected via dependency injection
- All validation attributes use resource keys instead of hardcoded ErrorMessage
- All Display(Name) attributes use Field.* resource keys

**Validation Localization Pattern:**
```csharp
// Before (hardcoded French)
[Required(ErrorMessage = "Le prénom est requis")]
[StringLength(100, MinimumLength = 2, ErrorMessage = "Le prénom doit contenir entre 2 et 100 caractères")]
[Display(Name = "Prénom")]
public string FirstName { get; set; }

// After (localized via resources)
[Required]
[StringLength(100, MinimumLength = 2)]
[Display(Name = "Field.FirstName")]
public string FirstName { get; set; }
```

---

**Last updated**: 2026-01-25
**Current phase**: Phase 5 (Localization - COMPLETED)
**Status**: Production-ready MVP with complete FR localization and NL/EN infrastructure ready for translation
