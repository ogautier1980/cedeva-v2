using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Website.Features.ExpenseCategories.ViewModels;

/// <summary>Create/Edit form for a manageable expense category.</summary>
public class ExpenseCategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "ExpenseCategory.CategoryType")]
    public ExpenseCategoryType CategoryType { get; set; }

    [Display(Name = "ExpenseCategory.Budget")]
    [Range(0, 1000000, ErrorMessage = "Validation.Range")]
    public decimal? Budget { get; set; }

    /// <summary>The organisation's P.A.F. category: its type is fixed. Display only — the controller
    /// reads the stored flag, never a posted one.</summary>
    public bool IsRegistrationFees { get; set; }
}
