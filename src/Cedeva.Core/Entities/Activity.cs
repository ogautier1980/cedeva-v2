using System.ComponentModel.DataAnnotations;

using Cedeva.Core.Interfaces;

namespace Cedeva.Core.Entities;

public class Activity : AuditableEntity, IOrganisationScoped
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(100, ErrorMessage = "Validation.StringLength")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    public bool IsActive { get; set; }

    public decimal? PricePerDay { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Comma-separated list of allowed postal codes (e.g., "1000,1050,1060").
    /// If empty/null, all postal codes are allowed (unless excluded).
    /// </summary>
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? IncludedPostalCodes { get; set; }

    /// <summary>
    /// Comma-separated list of excluded postal codes (e.g., "9000,9999").
    /// If empty/null, no postal codes are excluded.
    /// </summary>
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? ExcludedPostalCodes { get; set; }

    /// <summary>Message affiché au parent quand son code postal n'est pas autorisé (Lot I, étape 4).</summary>
    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    public string? PostalCodeErrorMessage { get; set; }

    /// <summary>Nombre maximum d'enfants pouvant s'inscrire par jour (Lot I, étape 4). Null = pas de limite.</summary>
    public int? MaxChildrenPerDay { get; set; }

    /// <summary>Message affiché quand <see cref="MaxChildrenPerDay"/> est atteint (Lot I, étape 4).</summary>
    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    public string? FullMessage { get; set; }

    /// <summary>Lien externe vers le règlement d'ordre intérieur (PDF hébergé par l'organisation) (Lot I, étape 3).</summary>
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? RegulationLinkUrl { get; set; }

    /// <summary>Texte affiché à côté de la case à cocher d'acceptation du règlement (Lot I, étape 3).</summary>
    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    public string? RegulationAcceptanceText { get; set; }

    /// <summary>Début de la fenêtre d'affichage du formulaire public d'inscription (Lot I, étape 6). Null = pas de restriction.</summary>
    [DataType(DataType.Date)]
    public DateTime? PublicationStartDate { get; set; }

    /// <summary>Fin de la fenêtre d'affichage du formulaire public d'inscription (Lot I, étape 6). Null = pas de restriction.</summary>
    [DataType(DataType.Date)]
    public DateTime? PublicationEndDate { get; set; }

    /// <summary>Message affiché si le formulaire public n'est pas dans sa fenêtre d'affichage (Lot I, étape 6).</summary>
    [StringLength(300, ErrorMessage = "Validation.StringLength")]
    public string? NoActiveFormMessage { get; set; }

    /// <summary>URL de redirection après l'envoi du formulaire public, à la place de la page de confirmation standard (Lot I, étape 6).</summary>
    [StringLength(500, ErrorMessage = "Validation.StringLength")]
    public string? RedirectUrlAfterSubmit { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<ActivityDay> Days { get; set; } = new List<ActivityDay>();
    public ICollection<ActivityGroup> Groups { get; set; } = new List<ActivityGroup>();
    public ICollection<ActivityQuestion> AdditionalQuestions { get; set; } = new List<ActivityQuestion>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
