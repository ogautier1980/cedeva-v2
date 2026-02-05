using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Activity
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

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public ICollection<ActivityDay> Days { get; set; } = new List<ActivityDay>();
    public ICollection<ActivityGroup> Groups { get; set; } = new List<ActivityGroup>();
    public ICollection<ActivityQuestion> AdditionalQuestions { get; set; } = new List<ActivityQuestion>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
