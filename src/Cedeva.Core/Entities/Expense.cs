using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Expense
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }
}
