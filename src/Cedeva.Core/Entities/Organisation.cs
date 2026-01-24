using System.ComponentModel.DataAnnotations;

namespace Cedeva.Core.Entities;

public class Organisation
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<Parent> Parents { get; set; } = new List<Parent>();
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    public ICollection<CedevaUser> Users { get; set; } = new List<CedevaUser>();
}
