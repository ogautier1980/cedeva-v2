namespace Cedeva.Core.Interfaces;

/// <summary>
/// Marker for entities that belong directly to an organisation and are subject to the standard
/// multi-tenant global query filter (see CedevaDbContext.ApplyOrganisationFilter).
/// </summary>
public interface IOrganisationScoped
{
    int OrganisationId { get; }
}
