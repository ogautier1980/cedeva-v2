using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class Expense
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    /// <summary>
    /// Type de dépense:
    /// - Reimbursement: note de frais → montant AJOUTÉ au solde de l'animateur
    /// - PersonalConsumption: consommation perso → montant DÉDUIT du solde de l'animateur
    /// </summary>
    [Required]
    public ExpenseType ExpenseType { get; set; }

    [Required]
    public int TeamMemberId { get; set; }
    public TeamMember TeamMember { get; set; } = null!;

    public int? ActivityId { get; set; }
    public Activity? Activity { get; set; }
}
