using System.ComponentModel.DataAnnotations;
using Cedeva.Core.Enums;

namespace Cedeva.Core.Entities;

public class Booking : AuditableEntity
{
    public int Id { get; set; }

    [DataType(DataType.Date)]
    public DateTime BookingDate { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;

    [Required(ErrorMessage = "The {0} field is required.")]
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int? GroupId { get; set; }
    public ActivityGroup? Group { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsConfirmed { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsMedicalSheet { get; set; }

    /// <summary>
    /// Communication structurée belge générée automatiquement (format +++XXX/XXXX/XXXXX+++)
    /// </summary>
    public string? StructuredCommunication { get; set; }

    /// <summary>
    /// Montant total à payer (calculé: PricePerDay × nombre de jours)
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Montant déjà payé (somme des paiements)
    /// </summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// Statut de paiement de la réservation
    /// </summary>
    public PaymentStatus PaymentStatus { get; set; }

    public ICollection<BookingDay> Days { get; set; } = new List<BookingDay>();
    public ICollection<ActivityQuestionAnswer> QuestionAnswers { get; set; } = new List<ActivityQuestionAnswer>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
