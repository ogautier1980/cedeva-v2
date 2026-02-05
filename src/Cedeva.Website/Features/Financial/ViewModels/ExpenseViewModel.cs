using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.Financial.ViewModels;

public class ExpenseViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Label")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Field.Description")]
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Field.Amount")]
    [Range(0.01, 999999.99)]
    public decimal Amount { get; set; }

    [Display(Name = "Expense.Category")]
    [StringLength(50, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? Category { get; set; }

    [Display(Name = "Expense.ExpenseType")]
    public ExpenseType? ExpenseType { get; set; }

    /// <summary>
    /// ID combiné: TeamMemberId si assigné à un animateur,
    /// "OrganizationCard" ou "OrganizationCash" si assigné à l'organisation
    /// </summary>
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Expense.AssignedTo")]
    public string AssignedTo { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Expense.Date")]
    [DataType(DataType.Date)]
    public DateTime ExpenseDate { get; set; }

    public int ActivityId { get; set; }
}
