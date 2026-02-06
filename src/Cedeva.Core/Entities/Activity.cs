using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Activity : AuditableEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "The {0} field is required.")]
    public bool IsActive { get; set; }

    public decimal? PricePerDay { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Comma-separated list of allowed postal codes (e.g., "1000,1050,1060").
    /// If empty/null, all postal codes are allowed (unless excluded).
    /// </summary>
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? IncludedPostalCodes { get; set; }

    /// <summary>
    /// Comma-separated list of excluded postal codes (e.g., "9000,9999").
    /// If empty/null, no postal codes are excluded.
    /// </summary>
    [StringLength(500, ErrorMessage = "The field {0} must have between {2} and {1} characters.")]
    public string? ExcludedPostalCodes { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<ActivityDay> Days { get; set; } = new List<ActivityDay>();
    public ICollection<ActivityGroup> Groups { get; set; } = new List<ActivityGroup>();
    public ICollection<ActivityQuestion> AdditionalQuestions { get; set; } = new List<ActivityQuestion>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
