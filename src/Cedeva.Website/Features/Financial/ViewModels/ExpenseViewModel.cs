using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;
using Cedeva.Website.Validation;

namespace Cedeva.Website.Features.Financial.ViewModels;

public class ExpenseViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Label")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Field.Description")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Field.Amount")]
    [Range(0.01, 999999.99, ErrorMessage = "Validation.Range")]
    public decimal Amount { get; set; }

    /// <summary>Recette (true) ou Dépense (false) — voir <see cref="Cedeva.Core.Entities.Expense.IsIncome"/>.</summary>
    [Display(Name = "Expense.Direction")]
    public bool IsIncome { get; set; }

    [Display(Name = "Expense.Category")]
    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    public string? Category { get; set; }

    [Display(Name = "Expense.ExpenseType")]
    public ExpenseType? ExpenseType { get; set; }

    /// <summary>
    /// ID combiné: TeamMemberId si assigné à un animateur,
    /// "OrganizationCard" ou "OrganizationCash" si assigné à l'organisation
    /// </summary>
    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Expense.AssignedTo")]
    public string AssignedTo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Display(Name = "Expense.Date")]
    [DataType(DataType.Date)]
    public DateTime ExpenseDate { get; set; }

    public int ActivityId { get; set; }

    [Display(Name = "Financial.Attachment")]
    [AllowedExtensions(".jpg", ".jpeg", ".png", ".gif", ".pdf")]
    [MaxFileSize(10 * 1024 * 1024)]
    public IFormFile? ReceiptFile { get; set; }

    /// <summary>Existing receipt, shown on the edit form.</summary>
    public string? ReceiptUrl { get; set; }

    public bool RemoveReceipt { get; set; }
}
