using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Entities;

namespace Cedeva.Website.Features.ActivityManagement.ViewModels;

public class ExcursionExpensesViewModel
{
    public Excursion Excursion { get; set; } = null!;
    public Activity Activity { get; set; } = null!;
    public List<Expense> Expenses { get; set; } = new();

    // Form fields for adding new expense
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Label")]
    public string Label { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Amount")]
    [Range(0.01, 9999.99)]
    public decimal Amount { get; set; }

    [StringLength(50, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    [Display(Name = "Field.Category")]
    public string? Category { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.ExpenseDate")]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.OrganizationPaymentSource")]
    public string OrganizationPaymentSource { get; set; } = "OrganizationCard";

    // Summary calculations
    public decimal TotalRevenue => Excursion.Registrations.Count * Excursion.Cost;
    public decimal TotalExpenses => Expenses.Sum(e => e.Amount);
    public decimal NetBalance => TotalRevenue - TotalExpenses;
}
