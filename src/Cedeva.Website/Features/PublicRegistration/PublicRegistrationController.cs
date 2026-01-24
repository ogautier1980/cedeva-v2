using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.PublicRegistration.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cedeva.Website.Features.PublicRegistration;

[AllowAnonymous]
public class PublicRegistrationController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly IEmailService _emailService;

    public PublicRegistrationController(
        CedevaDbContext context,
        IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    // GET: PublicRegistration/SelectActivity?orgId=1
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

        TempData["OrganisationId"] = orgId;

        return View(viewModel);
    }

    // POST: PublicRegistration/SelectActivity
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SelectActivity(SelectActivityViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData["ActivityId"] = model.ActivityId;
        TempData.Keep("OrganisationId");

        return RedirectToAction(nameof(ParentInformation));
    }

    // GET: PublicRegistration/ParentInformation
    public IActionResult ParentInformation()
    {
        if (TempData["ActivityId"] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var viewModel = new ParentInformationViewModel
        {
            ActivityId = (int)TempData["ActivityId"]!
        };

        TempData.Keep("ActivityId");
        TempData.Keep("OrganisationId");

        return View(viewModel);
    }

    // POST: PublicRegistration/ParentInformation
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ParentInformation(ParentInformationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var organisationId = (int)TempData["OrganisationId"]!;

        // Check if parent already exists by email
        var existingParent = await _context.Parents
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Email == model.Email && p.OrganisationId == organisationId);

        int parentId;

        if (existingParent != null)
        {
            // Update existing parent
            existingParent.FirstName = model.FirstName;
            existingParent.LastName = model.LastName;
            existingParent.PhoneNumber = model.PhoneNumber;
            existingParent.MobilePhoneNumber = model.MobilePhoneNumber;
            existingParent.NationalRegisterNumber = model.NationalRegisterNumber;

            if (existingParent.Address != null)
            {
                existingParent.Address.Street = model.Street;
                existingParent.Address.PostalCode = model.PostalCode;
                existingParent.Address.City = model.City;
                existingParent.Address.Country = Country.Belgium;
            }
            else
            {
                existingParent.Address = new Address
                {
                    Street = model.Street,
                    PostalCode = model.PostalCode,
                    City = model.City,
                    Country = Country.Belgium
                };
            }

            await _context.SaveChangesAsync();
            parentId = existingParent.Id;
        }
        else
        {
            // Create new parent
            var address = new Address
            {
                Street = model.Street,
                PostalCode = model.PostalCode,
                City = model.City,
                Country = Country.Belgium
            };

            var parent = new Parent
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                MobilePhoneNumber = model.MobilePhoneNumber,
                NationalRegisterNumber = model.NationalRegisterNumber,
                Address = address,
                OrganisationId = organisationId
            };

            _context.Parents.Add(parent);
            await _context.SaveChangesAsync();
            parentId = parent.Id;
        }

        TempData["ParentId"] = parentId;
        TempData.Keep("ActivityId");
        TempData.Keep("OrganisationId");

        return RedirectToAction(nameof(ChildInformation));
    }

    // GET: PublicRegistration/ChildInformation
    public IActionResult ChildInformation()
    {
        if (TempData["ActivityId"] == null || TempData["ParentId"] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var viewModel = new ChildInformationViewModel
        {
            ActivityId = (int)TempData["ActivityId"]!,
            ParentId = (int)TempData["ParentId"]!
        };

        TempData.Keep("ActivityId");
        TempData.Keep("ParentId");
        TempData.Keep("OrganisationId");

        return View(viewModel);
    }

    // POST: PublicRegistration/ChildInformation
    [HttpPost]
    [ValidateAntiForgeryToken]
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
                NationalRegisterNumber = model.NationalRegisterNumber,
                IsDisadvantagedEnvironment = model.IsDisadvantagedEnvironment,
                IsMildDisability = model.IsMildDisability,
                IsSevereDisability = model.IsSevereDisability,
                ParentId = model.ParentId
            };

            _context.Children.Add(child);
            await _context.SaveChangesAsync();
            childId = child.Id;
        }

        TempData["ChildId"] = childId;
        TempData.Keep("ActivityId");
        TempData.Keep("ParentId");
        TempData.Keep("OrganisationId");

        return RedirectToAction(nameof(ActivityQuestions));
    }

    // GET: PublicRegistration/ActivityQuestions
    public async Task<IActionResult> ActivityQuestions()
    {
        if (TempData["ActivityId"] == null || TempData["ParentId"] == null || TempData["ChildId"] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var activityId = (int)TempData["ActivityId"]!;

        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == activityId)
            .OrderBy(q => q.Id)
            .ToListAsync();

        var viewModel = new ActivityQuestionsViewModel
        {
            ActivityId = activityId,
            ParentId = (int)TempData["ParentId"]!,
            ChildId = (int)TempData["ChildId"]!,
            Questions = questions
        };

        TempData.Keep("ActivityId");
        TempData.Keep("ParentId");
        TempData.Keep("ChildId");
        TempData.Keep("OrganisationId");

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
    public async Task<IActionResult> ActivityQuestions(ActivityQuestionsViewModel model)
    {
        // Validate required questions
        var questions = await _context.ActivityQuestions
            .Where(q => q.ActivityId == model.ActivityId && q.IsRequired)
            .ToListAsync();

        foreach (var question in questions)
        {
            if (!model.Answers.ContainsKey(question.Id) || string.IsNullOrWhiteSpace(model.Answers[question.Id]))
            {
                ModelState.AddModelError("", $"La question '{question.QuestionText}' est obligatoire.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.Questions = await _context.ActivityQuestions
                .Where(q => q.ActivityId == model.ActivityId)
                .OrderBy(q => q.Id)
                .ToListAsync();
            return View(model);
        }

        // Store answers in TempData
        TempData["QuestionAnswers"] = JsonSerializer.Serialize(model.Answers);
        TempData.Keep("ActivityId");
        TempData.Keep("ParentId");
        TempData.Keep("ChildId");
        TempData.Keep("OrganisationId");

        return RedirectToAction(nameof(CreateBooking));
    }

    // GET: PublicRegistration/CreateBooking
    public async Task<IActionResult> CreateBooking()
    {
        if (TempData["ActivityId"] == null || TempData["ParentId"] == null || TempData["ChildId"] == null)
        {
            return RedirectToAction(nameof(SelectActivity));
        }

        var activityId = (int)TempData["ActivityId"]!;
        var childId = (int)TempData["ChildId"]!;

        // Check if booking already exists
        var existingBooking = await _context.Bookings
            .AnyAsync(b => b.ActivityId == activityId && b.ChildId == childId);

        if (existingBooking)
        {
            ModelState.AddModelError("", "Une inscription existe déjà pour cet enfant et cette activité.");
            return RedirectToAction(nameof(SelectActivity));
        }

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

        // Save question answers if any
        if (TempData["QuestionAnswers"] != null)
        {
            var answersJson = TempData["QuestionAnswers"]!.ToString();
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
        var parent = await _context.Parents.FindAsync((int)TempData["ParentId"]!);
        var child = await _context.Children.FindAsync(childId);
        var activity = await _context.Activities.FindAsync(activityId);

        if (parent != null && child != null && activity != null)
        {
            await SendConfirmationEmail(parent, child, activity, booking);
        }

        return RedirectToAction(nameof(Confirmation), new { bookingId = booking.Id });
    }

    // GET: PublicRegistration/Confirmation/5
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

    // GET: PublicRegistration/EmbedCode?orgId=1
    [Authorize(Roles = "Admin,Coordinator")]
    public IActionResult EmbedCode(int orgId)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var iframeUrl = $"{baseUrl}/PublicRegistration/SelectActivity?orgId={orgId}";

        var embedCode = $@"<iframe src=""{iframeUrl}"" width=""100%"" height=""800"" frameborder=""0"" style=""border: 1px solid #ddd; border-radius: 8px;""></iframe>";

        ViewBag.EmbedCode = embedCode;
        ViewBag.IframeUrl = iframeUrl;

        return View();
    }
}
