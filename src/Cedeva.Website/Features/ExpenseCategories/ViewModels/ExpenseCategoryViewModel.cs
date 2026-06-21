using System.ComponentModel.DataAnnotations;

namespace Cedeva.Website.Features.ExpenseCategories.ViewModels;

/// <summary>Create/Edit form for a manageable expense category.</summary>
public class ExpenseCategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    [Display(Name = "Field.Name")]
    public string Name { get; set; } = string.Empty;
}
