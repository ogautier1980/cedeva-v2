using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Helpers;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.PublicRegistration.ViewModels;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Text.Json;

namespace Cedeva.Website.Features.PublicRegistration;

public class PublicRegistrationController : Controller
{
    private const string TempDataOrganisationId = "OrganisationId";
    private const string TempDataActivityId = "ActivityId";
    private const string TempDataParentId = "ParentId";
    private const string TempDataChildId = "ChildId";
    private const string TempDataQuestionAnswers = "QuestionAnswers";

    private readonly CedevaDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public PublicRegistrationController(
        CedevaDbContext context,
        IEmailService emailService,
        IStringLocalizer<SharedResources> localizer)
    {
        _context = context;
        _emailService = emailService;
        _localizer = localizer;
    }

    // GET: PublicRegistration/SelectActivity?orgId=1
    [AllowAnonymous]
    public async Task<IActionResult> SelectActivity(int orgId)
    {
        var activities = await _context.Activities
            .Where(a => a.OrganisationId == orgId && a.StartDate > DateTime.Now)
            .OrderBy(a => a.StartDate)
            .ToListAsync();

        var viewModel = new SelectActivityViewModel
        {
            AvailableActivities = activities
        };

        TempData[TempDataOrganisationId] = orgId;

        return View(viewModel);
    }

    // POST: PublicRegistration/SelectActivity
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public IActionResult SelectActivity(SelectActivityViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData[TempDataActivityId] = model.ActivityId;
        TempData.Keep(TempDataOrganisationId);

        return RedirectToAction(nameof(ParentInformation));
    }

    // GET: PublicRegistration/ParentInformation
    [AllowAnonymous]
    public IActionResult ParentInformation()
    {
        if (TempData[TempDataActivityId] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var viewModel = new ParentInformationViewModel
        {
            ActivityId = (int)TempData[TempDataActivityId]!
        };

        TempData.Keep(TempDataActivityId);
        TempData.Keep(TempDataOrganisationId);

        return View(viewModel);
    }

    // POST: PublicRegistration/ParentInformation
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> ParentInformation(ParentInformationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var organisationId = (int)TempData[TempDataOrganisationId]!;
        var activityId = (int)TempData[TempDataActivityId]!;

        // Validate postal code against activity restrictions
        var postalCodeValidation = await ValidatePostalCodeAsync(activityId, model.PostalCode);
        if (!postalCodeValidation.IsValid)
        {
            ModelState.AddModelError(nameof(model.PostalCode), postalCodeValidation.ErrorMessage!);
            return View(model);
        }

        // Create or update parent
        var parentId = await CreateOrUpdateParentAsync(model, organisationId);

        TempData[TempDataParentId] = parentId;
        TempData.Keep(TempDataActivityId);
        TempData.Keep(TempDataOrganisationId);

        return RedirectToAction(nameof(ChildInformation));
    }

    // GET: PublicRegistration/ChildInformation
    [AllowAnonymous]
    public IActionResult ChildInformation()
    {
        if (TempData[TempDataActivityId] == null || TempData[TempDataParentId] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var viewModel = new ChildInformationViewModel
        {
            ActivityId = (int)TempData[TempDataActivityId]!,
            ParentId = (int)TempData[TempDataParentId]!
        };

        TempData.Keep(TempDataActivityId);
        TempData.Keep(TempDataParentId);
        TempData.Keep(TempDataOrganisationId);

        return View(viewModel);
    }

    // POST: PublicRegistration/ChildInformation
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> ChildInformation(ChildInformationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Check if child already exists
        var existingChild = await _context.Children
            .FirstOrDefaultAsync(c => c.NationalRegisterNumber == model.NationalRegisterNumber && c.ParentId == model.ParentId);

        int childId;

        if (existingChild != null)
        {
            // Update existing child
            existingChild.FirstName = model.FirstName;
            existingChild.LastName = model.LastName;
            existingChild.BirthDate = model.BirthDate;
            existingChild.IsDisadvantagedEnvironment = model.IsDisadvantagedEnvironment;
            existingChild.IsMildDisability = model.IsMildDisability;
            existingChild.IsSevereDisability = model.IsSevereDisability;

            await _context.SaveChangesAsync();
            childId = existingChild.Id;
        }
        else
        {
            // Create new child
            var child = new Child
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                BirthDate = model.BirthDate,
                NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(model.NationalRegisterNumber),
                IsDisadvantagedEnvironment = model.IsDisadvantagedEnvironment,
                IsMildDisability = model.IsMildDisability,
                IsSevereDisability = model.IsSevereDisability,
                ParentId = model.ParentId
            };

            _context.Children.Add(child);
            await _context.SaveChangesAsync();
            childId = child.Id;
        }

        TempData[TempDataChildId] = childId;
        TempData.Keep(TempDataActivityId);
        TempData.Keep(TempDataParentId);
        TempData.Keep(TempDataOrganisationId);

        return RedirectToAction(nameof(ActivityQuestions));
    }

    // GET: PublicRegistration/ActivityQuestions
    [AllowAnonymous]
    public async Task<IActionResult> ActivityQuestions()
    {
        if (TempData[TempDataActivityId] == null || TempData[TempDataParentId] == null || TempData[TempDataChildId] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var activityId = (int)TempData[TempDataActivityId]!;

        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId && q.IsActive)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync();

        var viewModel = new ActivityQuestionsViewModel
        {
            ActivityId = activityId,
            ParentId = (int)TempData[TempDataParentId]!,
            ChildId = (int)TempData[TempDataChildId]!,
            Questions = questions
        };

        TempData.Keep(TempDataActivityId);
        TempData.Keep(TempDataParentId);
        TempData.Keep(TempDataChildId);
        TempData.Keep(TempDataOrganisationId);

        // If no questions, skip to confirmation
        if (!questions.Any())
        {
            return RedirectToAction(nameof(CreateBooking));
        }

        return View(viewModel);
    }

    // POST: PublicRegistration/ActivityQuestions
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> ActivityQuestions(ActivityQuestionsViewModel model)
    {
        // Validate required questions
        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == model.ActivityId && q.IsRequired)
            .ToListAsync();

        var missingQuestions = questions
            .Where(q => !model.Answers.ContainsKey(q.Id) || string.IsNullOrWhiteSpace(model.Answers[q.Id]))
            .ToList();

        foreach (var question in missingQuestions)
        {
            ModelState.AddModelError("", $"La question '{question.QuestionText}' est obligatoire.");
        }

        if (!ModelState.IsValid)
        {
            model.Questions = await _context.ActivityQuestions
                .Where(q => q.ActivityId == model.ActivityId && q.IsActive)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();
            return View(model);
        }

        // Store answers in TempData
        TempData[TempDataQuestionAnswers] = JsonSerializer.Serialize(model.Answers);
        TempData.Keep(TempDataActivityId);
        TempData.Keep(TempDataParentId);
        TempData.Keep(TempDataChildId);
        TempData.Keep(TempDataOrganisationId);

        return RedirectToAction(nameof(CreateBooking));
    }

    // GET: PublicRegistration/CreateBooking
    [AllowAnonymous]
    public async Task<IActionResult> CreateBooking()
    {
        if (TempData[TempDataActivityId] == null || TempData[TempDataParentId] == null || TempData[TempDataChildId] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var activityId = (int)TempData[TempDataActivityId]!;
        var childId = (int)TempData[TempDataChildId]!;

        // Check if booking already exists
        var existingBooking = await _context.Bookings
            .AnyAsync(b => b.ActivityId == activityId && b.ChildId == childId);

        if (existingBooking)
        {
            ModelState.AddModelError("", _localizer["Message.BookingAlreadyExists"]);
            return RedirectToAction(nameof(SelectActivity));
        }

        // Get active days for the activity
        var activeDays = await _context.ActivityDays
            .Where(d => d.ActivityId == activityId && d.IsActive)
            .ToListAsync();

        // Create booking
        var booking = new Booking
        {
            ActivityId = activityId,
            ChildId = childId,
            BookingDate = DateTime.Now,
            IsConfirmed = false,
            IsMedicalSheet = false
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Add all active days to the booking
        foreach (var activityDay in activeDays)
        {
            var bookingDay = new BookingDay
            {
                BookingId = booking.Id,
                ActivityDayId = activityDay.DayId,
                IsReserved = true,
                IsPresent = false
            };
            _context.BookingDays.Add(bookingDay);
        }
        await _context.SaveChangesAsync();

        // Save question answers if any
        if (TempData[TempDataQuestionAnswers] != null)
        {
            var answersJson = TempData[TempDataQuestionAnswers]!.ToString();
            var answers = JsonSerializer.Deserialize<Dictionary<int, string>>(answersJson!);

            if (answers != null)
            {
                foreach (var answer in answers)
                {
                    var questionAnswer = new ActivityQuestionAnswer
                    {
                        BookingId = booking.Id,
                        ActivityQuestionId = answer.Key,
                        AnswerText = answer.Value
                    };
                    _context.ActivityQuestionAnswers.Add(questionAnswer);
                }
                await _context.SaveChangesAsync();
            }
        }

        // Send confirmation email
        var parent = await _context.Parents.FindAsync((int)TempData[TempDataParentId]!);
        var child = await _context.Children.FindAsync(childId);
        var activity = await _context.Activities.FindAsync(activityId);

        if (parent != null && child != null && activity != null)
        {
            await SendConfirmationEmail(parent, child, activity, booking);
        }

        return RedirectToAction(nameof(Confirmation), new { bookingId = booking.Id });
    }

    // GET: PublicRegistration/Confirmation/5
    [AllowAnonymous]
    public async Task<IActionResult> Confirmation(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Activity)
            .Include(b => b.Child)
                .ThenInclude(c => c.Parent)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return NotFound();
        }

        var viewModel = new ConfirmationViewModel
        {
            Booking = booking,
            Activity = booking.Activity,
            Parent = booking.Child.Parent,
            Child = booking.Child
        };

        return View(viewModel);
    }

    private async Task SendConfirmationEmail(Parent parent, Child child, Activity activity, Booking booking)
    {
        var subject = $"Confirmation d'inscription - {activity.Name}";
        var body = $@"
            <h2>Confirmation d'inscription</h2>
            <p>Bonjour {parent.FirstName} {parent.LastName},</p>
            <p>Nous avons bien reçu votre inscription pour <strong>{child.FirstName} {child.LastName}</strong> à l'activité <strong>{activity.Name}</strong>.</p>
            <h3>Détails de l'activité:</h3>
            <ul>
                <li><strong>Nom:</strong> {activity.Name}</li>
                <li><strong>Description:</strong> {activity.Description}</li>
                <li><strong>Date de début:</strong> {activity.StartDate:dd/MM/yyyy}</li>
                <li><strong>Date de fin:</strong> {activity.EndDate:dd/MM/yyyy}</li>
                {(activity.PricePerDay.HasValue ? $"<li><strong>Prix par jour:</strong> {activity.PricePerDay.Value:C}</li>" : "")}
            </ul>
            <p>Votre inscription sera confirmée prochainement par notre équipe.</p>
            <p><strong>Numéro de réservation:</strong> {booking.Id}</p>
            <p>Cordialement,<br/>L'équipe Cedeva</p>
        ";

        await _emailService.SendEmailAsync(parent.Email, subject, body);
    }

    // GET: PublicRegistration/Register?activityId=1&bg=ffffff
    [AllowAnonymous]
    public async Task<IActionResult> Register(int activityId, string? bg)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.StartDate > DateTime.Now);

        if (activity == null)
        {
            return NotFound();
        }

        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId && q.IsActive)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync();

        var viewModel = new SimpleRegistrationViewModel
        {
            ActivityId = activityId,
            ActivityName = activity.Name,
            ActivityDescription = activity.Description,
            ActivityStartDate = activity.StartDate,
            ActivityEndDate = activity.EndDate,
            PricePerDay = activity.PricePerDay
        };

        ViewBag.Questions = questions;
        ViewBag.BackgroundColor = bg ?? "ffffff";

        return View(viewModel);
    }

    // POST: PublicRegistration/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Register(SimpleRegistrationViewModel model, string? bg)
    {
        // Load and validate questions
        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == model.ActivityId)
            .ToListAsync();

        ValidateRequiredQuestions(questions, model);

        if (!ModelState.IsValid)
        {
            await ReloadModelWithActivityInfoAsync(model);
            ViewBag.Questions = questions;
            ViewBag.BackgroundColor = bg ?? "ffffff";
            return View(model);
        }

        var activityEntity = await _context.Activities.FindAsync(model.ActivityId);
        if (activityEntity == null)
        {
            return NotFound();
        }

        // Create or update parent and child
        var parentId = await CreateOrUpdateParentAsync(model, activityEntity.OrganisationId);
        var childId = await CreateOrUpdateChildAsync(model, parentId);

        // Check if booking already exists
        var existingBooking = await _context.Bookings
            .AnyAsync(b => b.ActivityId == model.ActivityId && b.ChildId == childId);

        if (existingBooking)
        {
            ModelState.AddModelError("", _localizer["Message.BookingAlreadyExists"]);
            await ReloadModelWithActivityInfoAsync(model);
            ViewBag.Questions = questions;
            ViewBag.BackgroundColor = bg ?? "ffffff";
            return View(model);
        }

        // Create booking with answers
        var bookingId = await CreateBookingWithAnswersAsync(model, childId, questions);

        // Send confirmation email
        var parent = await _context.Parents.FindAsync(parentId);
        var child = await _context.Children.FindAsync(childId);
        var booking = await _context.Bookings.FindAsync(bookingId);

        if (parent != null && child != null && booking != null)
        {
            await SendConfirmationEmail(parent, child, activityEntity, booking);
        }

        return RedirectToAction(nameof(Confirmation), new { bookingId });
    }

    private void ValidateRequiredQuestions(List<ActivityQuestion> questions, SimpleRegistrationViewModel model)
    {
        var requiredQuestions = questions.Where(q => q.IsRequired).ToList();
        foreach (var question in requiredQuestions)
        {
            if (!model.QuestionAnswers.ContainsKey(question.Id) ||
                string.IsNullOrWhiteSpace(model.QuestionAnswers[question.Id]))
            {
                ModelState.AddModelError("", $"{_localizer["PublicRegistration.QuestionRequired"]}: {question.QuestionText}");
            }
        }
    }

    private async Task ReloadModelWithActivityInfoAsync(SimpleRegistrationViewModel model)
    {
        var activity = await _context.Activities.FindAsync(model.ActivityId);
        if (activity != null)
        {
            model.ActivityName = activity.Name;
            model.ActivityDescription = activity.Description;
            model.ActivityStartDate = activity.StartDate;
            model.ActivityEndDate = activity.EndDate;
            model.PricePerDay = activity.PricePerDay;
        }
    }

    private async Task<int> CreateOrUpdateParentAsync(SimpleRegistrationViewModel model, int organisationId)
    {
        var existingParent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Email == model.ParentEmail && p.OrganisationId == organisationId);

        if (existingParent != null)
        {
            existingParent.FirstName = model.ParentFirstName;
            existingParent.LastName = model.ParentLastName;
            existingParent.PhoneNumber = model.ParentPhoneNumber;
            existingParent.MobilePhoneNumber = model.ParentMobilePhoneNumber ?? string.Empty;
            existingParent.NationalRegisterNumber = model.ParentNationalRegisterNumber ?? string.Empty;

            if (existingParent.Address != null)
            {
                existingParent.Address.Street = model.ParentStreet;
                existingParent.Address.PostalCode = model.ParentPostalCode;
                existingParent.Address.City = model.ParentCity;
            }
            else
            {
                existingParent.Address = new Address
                {
                    Street = model.ParentStreet,
                    PostalCode = model.ParentPostalCode,
                    City = model.ParentCity,
                    Country = Country.Belgium
                };
            }
            await _context.SaveChangesAsync();
            return existingParent.Id;
        }
        else
        {
            var address = new Address
            {
                Street = model.ParentStreet,
                PostalCode = model.ParentPostalCode,
                City = model.ParentCity,
                Country = Country.Belgium
            };

            var newParent = new Parent
            {
                FirstName = model.ParentFirstName,
                LastName = model.ParentLastName,
                Email = model.ParentEmail,
                PhoneNumber = model.ParentPhoneNumber,
                MobilePhoneNumber = model.ParentMobilePhoneNumber ?? string.Empty,
                NationalRegisterNumber = model.ParentNationalRegisterNumber ?? string.Empty,
                Address = address,
                OrganisationId = organisationId
            };

            _context.Parents.Add(newParent);
            await _context.SaveChangesAsync();
            return newParent.Id;
        }
    }

    private async Task<int> CreateOrUpdateChildAsync(SimpleRegistrationViewModel model, int parentId)
    {
        var existingChild = await _context.Children
            .FirstOrDefaultAsync(c => c.NationalRegisterNumber == model.ChildNationalRegisterNumber && c.ParentId == parentId);

        if (existingChild != null)
        {
            existingChild.FirstName = model.ChildFirstName;
            existingChild.LastName = model.ChildLastName;
            existingChild.BirthDate = model.ChildBirthDate;
            existingChild.IsDisadvantagedEnvironment = model.IsDisadvantagedEnvironment;
            existingChild.IsMildDisability = model.IsMildDisability;
            existingChild.IsSevereDisability = model.IsSevereDisability;
            await _context.SaveChangesAsync();
            return existingChild.Id;
        }
        else
        {
            var newChild = new Child
            {
                FirstName = model.ChildFirstName,
                LastName = model.ChildLastName,
                BirthDate = model.ChildBirthDate,
                NationalRegisterNumber = model.ChildNationalRegisterNumber,
                IsDisadvantagedEnvironment = model.IsDisadvantagedEnvironment,
                IsMildDisability = model.IsMildDisability,
                IsSevereDisability = model.IsSevereDisability,
                ParentId = parentId
            };
            _context.Children.Add(newChild);
            await _context.SaveChangesAsync();
            return newChild.Id;
        }
    }

    private async Task<int> CreateBookingWithAnswersAsync(SimpleRegistrationViewModel model, int childId, List<ActivityQuestion> questions)
    {
        var booking = new Booking
        {
            ActivityId = model.ActivityId,
            ChildId = childId,
            BookingDate = DateTime.Now,
            IsConfirmed = false,
            IsMedicalSheet = false
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Save question answers
        foreach (var answer in model.QuestionAnswers.Where(a => !string.IsNullOrWhiteSpace(a.Value)))
        {
            var questionAnswer = new ActivityQuestionAnswer
            {
                BookingId = booking.Id,
                ActivityQuestionId = answer.Key,
                AnswerText = answer.Value
            };
            _context.ActivityQuestionAnswers.Add(questionAnswer);
        }
        await _context.SaveChangesAsync();

        return booking.Id;
    }

    // GET: PublicRegistration/EmbedCode/1
    [Authorize(Roles = "Admin,Coordinator")]
    public async Task<IActionResult> EmbedCode(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
        {
            return NotFound();
        }

        ViewBag.ActivityId = id;
        ViewBag.ActivityName = activity.Name;

        return View();
    }

    /// <summary>
    /// Validates postal code against activity inclusion/exclusion rules.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> ValidatePostalCodeAsync(int activityId, string? postalCode)
    {
        var activity = await _context.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null)
        {
            return (true, null);
        }

        var trimmedPostalCode = postalCode?.Trim();

        // Check inclusion list (if defined, postal code MUST be in it)
        if (!string.IsNullOrWhiteSpace(activity.IncludedPostalCodes))
        {
            var includedCodes = activity.IncludedPostalCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToList();

            if (!includedCodes.Contains(trimmedPostalCode, StringComparer.OrdinalIgnoreCase))
            {
                return (false, _localizer["Registration.PostalCodeNotAllowed"].ToString());
            }
        }

        // Check exclusion list (postal code must NOT be in it)
        if (!string.IsNullOrWhiteSpace(activity.ExcludedPostalCodes))
        {
            var excludedCodes = activity.ExcludedPostalCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToList();

            if (excludedCodes.Contains(trimmedPostalCode, StringComparer.OrdinalIgnoreCase))
            {
                return (false, _localizer["Registration.PostalCodeExcluded"].ToString());
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Creates a new parent or updates existing parent by email.
    /// </summary>
    private async Task<int> CreateOrUpdateParentAsync(ParentInformationViewModel model, int organisationId)
    {
        var existingParent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Email == model.Email && p.OrganisationId == organisationId);

        if (existingParent != null)
        {
            return await UpdateExistingParentAsync(existingParent, model);
        }

        return await CreateNewParentAsync(model, organisationId);
    }

    /// <summary>
    /// Updates an existing parent with new information.
    /// </summary>
    private async Task<int> UpdateExistingParentAsync(Parent existingParent, ParentInformationViewModel model)
    {
        existingParent.FirstName = model.FirstName;
        existingParent.LastName = model.LastName;
        existingParent.PhoneNumber = model.PhoneNumber;
        existingParent.MobilePhoneNumber = model.MobilePhoneNumber;
        existingParent.NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(model.NationalRegisterNumber);

        if (existingParent.Address != null)
        {
            existingParent.Address.Street = model.Street ?? string.Empty;
            existingParent.Address.PostalCode = model.PostalCode ?? string.Empty;
            existingParent.Address.City = model.City ?? string.Empty;
            existingParent.Address.Country = Country.Belgium;
        }
        else
        {
            existingParent.Address = CreateAddressFromModel(model);
        }

        await _context.SaveChangesAsync();
        return existingParent.Id;
    }

    /// <summary>
    /// Creates a new parent with address.
    /// </summary>
    private async Task<int> CreateNewParentAsync(ParentInformationViewModel model, int organisationId)
    {
        var parent = new Parent
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            MobilePhoneNumber = model.MobilePhoneNumber,
            NationalRegisterNumber = NationalRegisterNumberHelper.StripFormatting(model.NationalRegisterNumber),
            Address = CreateAddressFromModel(model),
            OrganisationId = organisationId
        };

        _context.Parents.Add(parent);
        await _context.SaveChangesAsync();
        return parent.Id;
    }

    /// <summary>
    /// Creates an Address entity from parent information viewmodel.
    /// </summary>
    private static Address CreateAddressFromModel(ParentInformationViewModel model)
    {
        return new Address
        {
            Street = model.Street ?? string.Empty,
            PostalCode = model.PostalCode ?? string.Empty,
            City = model.City ?? string.Empty,
            Country = Country.Belgium
        };
    }
}
