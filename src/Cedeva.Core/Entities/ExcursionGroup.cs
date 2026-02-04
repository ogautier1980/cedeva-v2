namespace Cedeva.Core.Entities;

/// <summary>
/// Table de jonction entre Excursion et ActivityGroup (many-to-many)
/// Permet de cibler plusieurs groupes pour une même excursion
/// </summary>
public class ExcursionGroup
{
    public int ExcursionId { get; set; }
    public Excursion Excursion { get; set; } = null!;

    public int ActivityGroupId { get; set; }
    public ActivityGroup ActivityGroup { get; set; } = null!;
}
