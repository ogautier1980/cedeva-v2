namespace Cedeva.Website.Infrastructure;

/// <summary>
/// Base class for query parameters with common pagination and sorting fields.
/// </summary>
public abstract class QueryParametersBase
{
    public string? SearchString { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; } = "asc";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Query parameters for Bookings Index.
/// </summary>
public class BookingQueryParameters : QueryParametersBase
{
    public int? ActivityId { get; set; }
    public int? ChildId { get; set; }
    public bool? IsConfirmed { get; set; }
    public int? OrganisationId { get; set; }
}

/// <summary>
/// Query parameters for Users Index.
/// </summary>
public class UserQueryParameters : QueryParametersBase
{
    public string? RoleFilter { get; set; }
    public int? OrganisationId { get; set; }
    public bool? IsLockedOut { get; set; }
    public bool? EmailConfirmed { get; set; }
}

/// <summary>
/// Query parameters for Organisations Index.
/// </summary>
public class OrganisationQueryParameters : QueryParametersBase
{
    public bool? IsActive { get; set; }
    public string? City { get; set; }
}

/// <summary>
/// Query parameters for Activities Index.
/// </summary>
public class ActivityQueryParameters : QueryParametersBase
{
    public bool? ShowActiveOnly { get; set; }
}

/// <summary>
/// Query parameters for Children Index.
/// </summary>
public class ChildQueryParameters : QueryParametersBase
{
    public int? OrganisationId { get; set; }
}

/// <summary>
/// Query parameters for Parents Index.
/// </summary>
public class ParentQueryParameters : QueryParametersBase
{
    // Additional parent-specific filters can be added here
}

/// <summary>
/// Query parameters for TeamMembers Index.
/// </summary>
public class TeamMemberQueryParameters : QueryParametersBase
{
    public string? RoleFilter { get; set; }
}
