using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class Expense : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Label { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public decimal Amount { get; set; }

    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    public string? Category { get; set; }

    /// <summary>
    /// Type de dépense (uniquement pour dépenses liées à un animateur):
    /// - Reimbursement: note de frais → montant AJOUTÉ au solde de l'animateur
    /// - PersonalConsumption: consommation perso → montant DÉDUIT du solde de l'animateur
    /// </summary>
    public ExpenseType? ExpenseType { get; set; }

    /// <summary>
    /// Animateur concerné (null si dépense d'organisation)
    /// </summary>
    public int? TeamMemberId { get; set; }
    public TeamMember? TeamMember { get; set; }

    /// <summary>
    /// Source de paiement pour les dépenses d'organisation (si TeamMemberId est null)
    /// Valeurs: "OrganizationCard" ou "OrganizationCash"
    /// </summary>
    [StringLength(50, ErrorMessage = "Validation.StringLength")]
    public string? OrganizationPaymentSource { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    /// <summary>
    /// Excursion concernée (null si dépense d'activité générale)
    /// </summary>
    public int? ExcursionId { get; set; }
    public Excursion? Excursion { get; set; }

    public DateTime ExpenseDate { get; set; }
}
