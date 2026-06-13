using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Website.Features.Financial.ViewModels;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Cedeva.Website.Features.Financial;

[Authorize(Roles = "Coordinator,Admin")]
public class FinancialController : Controller
{
    private readonly CedevaDbContext _context;
    private readonly IExcelExportService _excelExportService;
    private readonly IFinancialCalculationService _financialCalculationService;
    private readonly ICedevaControllerContext<FinancialController> _ctx;

    private const string SessionKeyActivityId = "Financial_ActivityId";
    private const string ActionIndex = "Index";
    private const string ControllerActivities = "Activities";
    private const string OrganizationCard = "OrganizationCard";
    private const string OrganizationCash = "OrganizationCash";

    public FinancialController(
        CedevaDbContext context,
        IExcelExportService excelExportService,
        IFinancialCalculationService financialCalculationService,
        ICedevaControllerContext<FinancialController> ctx)
    {
        _context = context;
        _excelExportService = excelExportService;
        _financialCalculationService = financialCalculationService;
        _ctx = ctx;
    }

    // POST: Financial/BeginFinancial
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BeginFinancial(int id)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        HttpContext.Session.SetInt32(SessionKeyActivityId, id);
        return RedirectToAction(nameof(Index));
    }

    // GET: Financial/Index
    public async Task<IActionResult> Index(int? id = null)
    {
        if (id.HasValue)
            HttpContext.Session.SetInt32(SessionKeyActivityId, id.Value);

        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.TeamMembers)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Payments)
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        // Load expenses for calculations
        var expenses = await _context.Expenses
            .Where(e => e.ActivityId == activityId.Value)
            .ToListAsync();

        // Calculate financial metrics using service
        var totalRevenue = _financialCalculationService.CalculateTotalRevenue(activity);
        var organizationExpenses = _financialCalculationService.CalculateOrganizationExpenses(expenses);
        var teamMemberExpenses = _financialCalculationService.CalculateTeamMemberSalaries(activity, expenses);
        var totalExpenses = _financialCalculationService.CalculateTotalExpenses(activity, expenses);
        var pendingAmount = _financialCalculationService.CalculatePendingPayments(activity);

        // Count pending bookings for display
        var pendingBookings = activity.Bookings
            .Where(b => !b.IsConfirmed)
            .ToList();

        var viewModel = new ActivityFinancialDashboardViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            Balance = totalRevenue - totalExpenses,
            PendingPaymentsCount = pendingBookings.Count,
            PendingPaymentsAmount = pendingAmount,
            BookingsCount = activity.Bookings.Count,
            ConfirmedBookingsCount = activity.Bookings.Count(b => b.IsConfirmed),
            TeamMembersCount = activity.TeamMembers.Count,
            TeamMemberExpenses = teamMemberExpenses,
            OrganizationExpenses = organizationExpenses
        };

        return View(viewModel);
    }

    // GET: Financial/TeamSalaries
    public async Task<IActionResult> TeamSalaries()
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        var daysCount = activity.Days.Count;
        var teamSalaries = new List<TeamSalaryViewModel>();
        foreach (var teamMember in activity.TeamMembers)
            teamSalaries.Add(await BuildTeamSalaryViewModelAsync(teamMember, daysCount, activityId.Value));

        var viewModel = new TeamSalariesViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            TotalDays = daysCount,
            TeamSalaries = teamSalaries,
            TotalPrestations = teamSalaries.Sum(s => s.Prestations),
            TotalReimbursements = teamSalaries.Sum(s => s.Reimbursements),
            TotalPersonalConsumptions = teamSalaries.Sum(s => s.PersonalConsumptions),
            GrandTotal = teamSalaries.Sum(s => s.TotalToPay)
        };

        return View(viewModel);
    }

    private async Task<TeamSalaryViewModel> BuildTeamSalaryViewModelAsync(TeamMember teamMember, int daysCount, int activityId)
    {
        var expenses = await _context.Expenses
            .Where(e => e.TeamMemberId == teamMember.TeamMemberId && e.ActivityId == activityId)
            .ToListAsync();

        var totalToPay = _financialCalculationService.CalculateTeamMemberSalary(teamMember, daysCount, expenses);
        var reimbursements = expenses.Where(e => e.ExpenseType == Core.Enums.ExpenseType.Reimbursement).ToList();
        var personalConsumptions = expenses.Where(e => e.ExpenseType == Core.Enums.ExpenseType.PersonalConsumption).ToList();

        return new TeamSalaryViewModel
        {
            TeamMemberId = teamMember.TeamMemberId,
            TeamMemberName = teamMember.FullName,
            Email = teamMember.Email,
            TeamRole = teamMember.TeamRole.ToString(),
            DaysCount = daysCount,
            DailyCompensation = teamMember.DailyCompensation ?? 0,
            Prestations = daysCount * (teamMember.DailyCompensation ?? 0),
            Reimbursements = reimbursements.Sum(e => e.Amount),
            ReimbursementsCount = reimbursements.Count,
            PersonalConsumptions = personalConsumptions.Sum(e => e.Amount),
            PersonalConsumptionsCount = personalConsumptions.Count,
            TotalToPay = totalToPay,
            ReimbursementDetails = reimbursements.Select(e => new ExpenseDetailViewModel { Id = e.Id, Label = e.Label, Amount = e.Amount }).ToList(),
            PersonalConsumptionDetails = personalConsumptions.Select(e => new ExpenseDetailViewModel { Id = e.Id, Label = e.Label, Amount = e.Amount }).ToList()
        };
    }

    // GET: Financial/ExportTeamSalaries
    public async Task<IActionResult> ExportTeamSalaries()
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.TeamMembers)
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        var teamSalaries = new List<TeamSalaryViewModel>();
        var daysCount = activity.Days.Count;

        foreach (var teamMember in activity.TeamMembers)
        {
            var expenses = await _context.Expenses
                .Where(e => e.TeamMemberId == teamMember.TeamMemberId && e.ActivityId == activityId.Value)
                .ToListAsync();

            var reimbursements = expenses.Where(e => e.ExpenseType == Core.Enums.ExpenseType.Reimbursement).ToList();
            var personalConsumptions = expenses.Where(e => e.ExpenseType == Core.Enums.ExpenseType.PersonalConsumption).ToList();

            var reimbursementsTotal = reimbursements.Sum(e => e.Amount);
            var personalConsumptionsTotal = personalConsumptions.Sum(e => e.Amount);
            var prestations = daysCount * (teamMember.DailyCompensation ?? 0);
            var totalToPay = prestations + reimbursementsTotal - personalConsumptionsTotal;

            teamSalaries.Add(new TeamSalaryViewModel
            {
                TeamMemberId = teamMember.TeamMemberId,
                TeamMemberName = teamMember.FullName,
                Email = teamMember.Email,
                TeamRole = teamMember.TeamRole.ToString(),
                DaysCount = daysCount,
                DailyCompensation = teamMember.DailyCompensation ?? 0,
                Prestations = prestations,
                Reimbursements = reimbursementsTotal,
                ReimbursementsCount = reimbursements.Count,
                PersonalConsumptions = personalConsumptionsTotal,
                PersonalConsumptionsCount = personalConsumptions.Count,
                TotalToPay = totalToPay
            });
        }

        // Définir les colonnes pour l'export Excel
        var columns = new Dictionary<string, Func<TeamSalaryViewModel, object>>
        {
            { _ctx.Localizer["Field.TeamMember"].Value, s => s.TeamMemberName },
            { _ctx.Localizer["Field.Email"].Value, s => s.Email },
            { _ctx.Localizer["Field.Role"].Value, s => _ctx.Localizer[$"TeamRole.{s.TeamRole}"].Value },
            { _ctx.Localizer["Financial.Days"].Value, s => s.DaysCount },
            { _ctx.Localizer["Financial.DailyCompensation"].Value, s => s.DailyCompensation },
            { _ctx.Localizer["Financial.Prestations"].Value, s => s.Prestations },
            { _ctx.Localizer["Financial.Reimbursements"].Value, s => s.Reimbursements },
            { _ctx.Localizer["Financial.ReimbursementsCount"].Value, s => s.ReimbursementsCount },
            { _ctx.Localizer["Financial.Consumptions"].Value, s => s.PersonalConsumptions },
            { _ctx.Localizer["Financial.ConsumptionsCount"].Value, s => s.PersonalConsumptionsCount },
            { _ctx.Localizer["Financial.TotalToPay"].Value, s => s.TotalToPay }
        };

        var excelData = _excelExportService.ExportToExcel(
            teamSalaries,
            _ctx.Localizer["Financial.TeamSalaries"].Value,
            columns
        );

        var fileName = $"Salaires_Equipe_{activity.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Financial/Expenses
    // GET: Financial/CreateExpense
    public async Task<IActionResult> CreateExpense()
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        await PopulateAssignedToDropdown(activityId.Value);

        return View(new ExpenseViewModel
        {
            ExpenseDate = DateTime.Today,
            ActivityId = activityId.Value
        });
    }

    // POST: Financial/CreateExpense
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(ExpenseViewModel viewModel)
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAssignedToDropdown(activityId.Value);
            return View(viewModel);
        }

        var expense = new Expense
        {
            Label = viewModel.Label,
            Description = viewModel.Description,
            Amount = viewModel.Amount,
            Category = viewModel.Category,
            ExpenseDate = viewModel.ExpenseDate,
            ActivityId = activityId.Value
        };

        // Parse AssignedTo
        if (viewModel.AssignedTo == OrganizationCard || viewModel.AssignedTo == OrganizationCash)
        {
            expense.OrganizationPaymentSource = viewModel.AssignedTo;
            expense.TeamMemberId = null;
            expense.ExpenseType = null;
        }
        else if (int.TryParse(viewModel.AssignedTo, out int teamMemberId))
        {
            expense.TeamMemberId = teamMemberId;
            expense.OrganizationPaymentSource = null;
            expense.ExpenseType = viewModel.ExpenseType ?? Core.Enums.ExpenseType.Reimbursement;
        }

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.ExpenseCreated"].Value;
        return RedirectToAction(nameof(Transactions));
    }

    // GET: Financial/EditExpense/5
    public async Task<IActionResult> EditExpense(int id, string? returnUrl = null)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == id);

        if (expense == null)
        {
            return NotFound();
        }

        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue || expense.ActivityId != activityId.Value)
        {
            return Forbid();
        }

        await PopulateAssignedToDropdown(expense.ActivityId);

        var viewModel = new ExpenseViewModel
        {
            Id = expense.Id,
            Label = expense.Label,
            Description = expense.Description,
            Amount = expense.Amount,
            Category = expense.Category,
            ExpenseDate = expense.ExpenseDate,
            ExpenseType = expense.ExpenseType,
            ActivityId = expense.ActivityId,
            AssignedTo = expense.TeamMemberId?.ToString()
                ?? expense.OrganizationPaymentSource
                ?? OrganizationCard
        };

        ViewData["ReturnUrl"] = returnUrl;
        return View(viewModel);
    }

    // POST: Financial/EditExpense/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExpense(int id, ExpenseViewModel viewModel, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (id != viewModel.Id)
        {
            return BadRequest();
        }

        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
        {
            return NotFound();
        }

        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue || expense.ActivityId != activityId.Value)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateAssignedToDropdown(expense.ActivityId);
            return View(viewModel);
        }

        expense.Label = viewModel.Label;
        expense.Description = viewModel.Description;
        expense.Amount = viewModel.Amount;
        expense.Category = viewModel.Category;
        expense.ExpenseDate = viewModel.ExpenseDate;

        // Parse AssignedTo
        if (viewModel.AssignedTo == OrganizationCard || viewModel.AssignedTo == OrganizationCash)
        {
            expense.OrganizationPaymentSource = viewModel.AssignedTo;
            expense.TeamMemberId = null;
            expense.ExpenseType = null;
        }
        else if (int.TryParse(viewModel.AssignedTo, out int teamMemberId))
        {
            expense.TeamMemberId = teamMemberId;
            expense.OrganizationPaymentSource = null;
            expense.ExpenseType = viewModel.ExpenseType ?? Core.Enums.ExpenseType.Reimbursement;
        }

        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.ExpenseUpdated"].Value;

        // Redirect to return URL if provided, otherwise to Transactions
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Transactions));
    }

    // POST: Financial/DeleteExpense/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
        {
            return NotFound();
        }

        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue || expense.ActivityId != activityId.Value)
        {
            return Forbid();
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        TempData[ControllerExtensions.SuccessMessageKey] = _ctx.Localizer["Message.ExpenseDeleted"].Value;
        return RedirectToAction(nameof(Transactions));
    }

    // GET: Financial/ExportExpenses
    public async Task<IActionResult> ExportExpenses()
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        var expenses = await _context.Expenses
            .Include(e => e.TeamMember)
            .Where(e => e.ActivityId == activityId.Value)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();

        var expenseListItems = expenses.Select(e => new
        {
            Date = e.ExpenseDate,
            Label = e.Label,
            Description = e.Description ?? "",
            Category = e.Category ?? "",
            Amount = e.Amount,
            AssignedTo = GetExpenseAssignedToLabel(e),
            Type = e.ExpenseType.HasValue
                ? _ctx.Localizer[$"ExpenseType.{e.ExpenseType}"].Value
                : ""
        }).ToList();

        var columns = new Dictionary<string, Func<dynamic, object>>
        {
            { _ctx.Localizer["Expense.Date"].Value, e => e.Date },
            { _ctx.Localizer["Field.Label"].Value, e => e.Label },
            { _ctx.Localizer["Field.Description"].Value, e => e.Description },
            { _ctx.Localizer["Expense.Category"].Value, e => e.Category },
            { _ctx.Localizer["Expense.AssignedTo"].Value, e => e.AssignedTo },
            { _ctx.Localizer["Expense.Type"].Value, e => e.Type },
            { _ctx.Localizer["Field.Amount"].Value, e => e.Amount }
        };

        var excelData = _excelExportService.ExportToExcel(
            expenseListItems,
            _ctx.Localizer["Expense.Management"].Value,
            columns
        );

        var fileName = $"Depenses_{activity.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // GET: Financial/Transactions
    public async Task<IActionResult> Transactions(string? filter)
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        var transactions = new List<ViewModels.TransactionViewModel>();

        // Récupérer les paiements (entrées) si pas de filtre ou filtre "income"
        if (string.IsNullOrEmpty(filter) || filter == "income")
        {
            var payments = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Child)
                .Where(p => p.Booking.ActivityId == activityId.Value && p.Status == Core.Enums.PaymentStatus.Paid)
                .ToListAsync();

            transactions.AddRange(payments.Select(p => new ViewModels.TransactionViewModel
            {
                Date = p.PaymentDate,
                Type = "Payment",
                Label = $"{_ctx.Localizer["Payments.PaymentFrom"]} {p.Booking.Child.FirstName} {p.Booking.Child.LastName}",
                Amount = p.Amount,
                IsIncome = true,
                PaymentMethod = _ctx.Localizer[$"PaymentMethod.{p.PaymentMethod}"].Value,
                ChildName = $"{p.Booking.Child.FirstName} {p.Booking.Child.LastName}",
                RelatedId = p.Id
            }));
        }

        // Récupérer les dépenses (sorties) si pas de filtre ou filtre "expense"
        if (string.IsNullOrEmpty(filter) || filter == "expense")
        {
            var expenses = await _context.Expenses
                .Include(e => e.TeamMember)
                .Include(e => e.Excursion)
                .Where(e => e.ActivityId == activityId.Value)
                .ToListAsync();

            transactions.AddRange(expenses.Select(e => new ViewModels.TransactionViewModel
            {
                Date = e.ExpenseDate,
                Type = "Expense",
                Label = e.Label,
                Category = e.Category,
                AssignedTo = GetExpenseAssignedToLabel(e),
                Amount = e.Amount,
                IsIncome = false,
                RelatedId = e.Id,
                ExcursionName = e.Excursion?.Name
            }));
        }

        // Trier par date décroissante
        transactions = transactions.OrderByDescending(t => t.Date).ToList();

        var totalIncome = transactions.Where(t => t.IsIncome).Sum(t => t.Amount);
        var totalExpenses = transactions.Where(t => !t.IsIncome).Sum(t => t.Amount);

        var viewModel = new ViewModels.TransactionsListViewModel
        {
            ActivityName = activity.Name,
            ActivityId = activity.Id,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetBalance = totalIncome - totalExpenses,
            Transactions = transactions
        };

        ViewBag.CurrentFilter = filter;
        return View(viewModel);
    }

    // GET: Financial/Report
    public async Task<IActionResult> Report()
    {
        var activityId = HttpContext.Session.GetInt32(SessionKeyActivityId);
        if (!activityId.HasValue)
        {
            return RedirectToAction(ActionIndex, ControllerActivities);
        }

        var activity = await _context.Activities
            .Include(a => a.Days)
            .Include(a => a.TeamMembers)
            .Include(a => a.Bookings)
                .ThenInclude(b => b.Payments)
            .FirstOrDefaultAsync(a => a.Id == activityId.Value);

        if (activity == null)
        {
            return NotFound();
        }

        var expenses = await _context.Expenses
            .Include(e => e.TeamMember)
            .Where(e => e.ActivityId == activityId.Value)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();

        var totalRevenue = _financialCalculationService.CalculateTotalRevenue(activity);
        var pendingAmount = _financialCalculationService.CalculatePendingPayments(activity);
        var confirmedBookings = activity.Bookings.Count(b => b.IsConfirmed);
        var pendingBookings = activity.Bookings.Count(b => !b.IsConfirmed);
        var avgRevenue = activity.Bookings.Any() ? totalRevenue / activity.Bookings.Count : 0;

        var (orgCardExpenses, orgCashExpenses, orgExpenseDetails) = BuildOrganizationExpenseBreakdown(expenses);
        var teamSalaryDetails = BuildTeamMemberSalaryDetails(activity, expenses);
        var totalTeamSalaries = _financialCalculationService.CalculateTeamMemberSalaries(activity, expenses);
        var totalExpenses = _financialCalculationService.CalculateTotalExpenses(activity, expenses);
        var balance = _financialCalculationService.CalculateNetProfit(activity, expenses);
        var balancePercentage = totalRevenue > 0 ? (balance / totalRevenue) * 100 : 0;

        var viewModel = new FinancialReportViewModel
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            TotalDays = activity.Days.Count,
            TotalRevenue = totalRevenue,
            TotalBookings = activity.Bookings.Count,
            ConfirmedBookings = confirmedBookings,
            PendingBookings = pendingBookings,
            PendingAmount = pendingAmount,
            AverageRevenuePerBooking = avgRevenue,
            OrganizationCardExpenses = orgCardExpenses,
            OrganizationCashExpenses = orgCashExpenses,
            TotalOrganizationExpenses = orgCardExpenses + orgCashExpenses,
            OrganizationExpenseDetails = orgExpenseDetails,
            TeamMembersCount = activity.TeamMembers.Count,
            TotalTeamSalaries = totalTeamSalaries,
            TeamMemberSalaryDetails = teamSalaryDetails,
            TotalExpenses = totalExpenses,
            Balance = balance,
            BalancePercentage = balancePercentage
        };

        return View(viewModel);
    }

    private static (decimal cardExpenses, decimal cashExpenses, List<ExpenseDetailViewModel> details) BuildOrganizationExpenseBreakdown(List<Expense> expenses)
    {
        var orgExpenses = expenses.Where(e => !e.TeamMemberId.HasValue).ToList();
        var cardExpenses = orgExpenses.Where(e => e.OrganizationPaymentSource == OrganizationCard).Sum(e => e.Amount);
        var cashExpenses = orgExpenses.Where(e => e.OrganizationPaymentSource == OrganizationCash).Sum(e => e.Amount);

        var details = orgExpenses.Select(e => new ExpenseDetailViewModel
        {
            Id = e.Id,
            Date = e.ExpenseDate,
            Label = e.Label,
            Category = e.Category ?? "",
            Amount = e.Amount
        }).ToList();

        return (cardExpenses, cashExpenses, details);
    }

    private List<TeamMemberSalaryDetailViewModel> BuildTeamMemberSalaryDetails(Activity activity, List<Expense> expenses)
    {
        var daysCount = activity.Days.Count;
        var teamSalaryDetails = new List<TeamMemberSalaryDetailViewModel>();

        foreach (var tm in activity.TeamMembers)
        {
            var tmExpenses = expenses.Where(e => e.TeamMemberId == tm.TeamMemberId);
            var netSalary = _financialCalculationService.CalculateTeamMemberSalary(tm, daysCount, tmExpenses);
            var baseSalary = daysCount * (tm.DailyCompensation ?? 0);
            var reimbursements = tmExpenses.Where(e => e.ExpenseType == Core.Enums.ExpenseType.Reimbursement).Sum(e => e.Amount);
            var consumptions = tmExpenses.Where(e => e.ExpenseType == Core.Enums.ExpenseType.PersonalConsumption).Sum(e => e.Amount);

            teamSalaryDetails.Add(new TeamMemberSalaryDetailViewModel
            {
                Name = tm.FullName,
                Role = _ctx.Localizer[$"TeamRole.{tm.TeamRole}"].Value,
                DaysWorked = daysCount,
                DailyCompensation = tm.DailyCompensation ?? 0,
                BaseSalary = baseSalary,
                Reimbursements = reimbursements,
                PersonalConsumptions = consumptions,
                NetSalary = netSalary
            });
        }

        return teamSalaryDetails;
    }

    private async Task PopulateAssignedToDropdown(int activityId)
    {
        var teamMembers = await _context.Activities
            .Where(a => a.Id == activityId)
            .SelectMany(a => a.TeamMembers)
            .OrderBy(tm => tm.LastName)
            .ThenBy(tm => tm.FirstName)
            .Select(tm => new
            {
                Value = tm.TeamMemberId.ToString(),
                Text = tm.FullName
            })
            .ToListAsync();

        var assignedToList = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
        {
            new() { Value = OrganizationCard, Text = _ctx.Localizer["Expense.OrganizationCard"].Value },
            new() { Value = OrganizationCash, Text = _ctx.Localizer["Expense.OrganizationCash"].Value }
        };

        assignedToList.AddRange(teamMembers.Select(tm => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
        {
            Value = tm.Value,
            Text = tm.Text
        }));

        ViewBag.AssignedToList = assignedToList;
    }

    private string GetExpenseAssignedToLabel(Expense e)
    {
        if (e.TeamMemberId.HasValue)
        {
            return e.TeamMember?.FullName ?? "";
        }
        return e.OrganizationPaymentSource == OrganizationCard
            ? _ctx.Localizer["Expense.OrganizationCard"].Value
            : _ctx.Localizer["Expense.OrganizationCash"].Value;
    }
}
