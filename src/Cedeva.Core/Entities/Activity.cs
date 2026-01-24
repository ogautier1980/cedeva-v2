using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Activity
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public bool IsActive { get; set; }

    public decimal? PricePerDay { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required]
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
