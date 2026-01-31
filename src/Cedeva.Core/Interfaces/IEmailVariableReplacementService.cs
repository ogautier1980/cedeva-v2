namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service for replacing variables in email templates with actual data
/// Variables use the pattern %variable_name%
/// </summary>
public interface IEmailVariableReplacementService
{
    /// <summary>
    /// Replaces all variables in the template with actual data from the booking
    /// </summary>
    /// <param name="template">Email template containing variables like %prenom_enfant%</param>
    /// <param name="booking">Booking entity with navigation properties loaded</param>
    /// <param name="organisation">Organisation entity</param>
    /// <returns>Template with variables replaced by actual values</returns>
    string ReplaceVariables(string template, Entities.Booking booking, Entities.Organisation organisation);

    /// <summary>
    /// Gets a dictionary of all available variables with their descriptions
    /// </summary>
    /// <returns>Dictionary with variable name as key and description as value</returns>
    Dictionary<string, string> GetAvailableVariables();
}
