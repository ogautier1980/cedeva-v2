using System.Text.RegularExpressions;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;

namespace Cedeva.Infrastructure.Services;

/// <summary>
/// Service for replacing variables in email templates with actual data
/// Supports 22 variables covering child, parent, booking, activity, and organization data
/// </summary>
public class EmailVariableReplacementService : IEmailVariableReplacementService
{
    /// <summary>
    /// Replaces all variables in the template with actual data
    /// Uses case-insensitive regex pattern: %variable_name%
    /// </summary>
    public string ReplaceVariables(string template, Booking booking, Organisation organisation)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        // Build variable resolver dictionary
        var variableResolvers = BuildVariableResolvers(booking, organisation);

        // Replace variables using regex (case-insensitive)
        var result = Regex.Replace(
            template,
            @"%(\w+)%",
            match =>
            {
                var variableName = match.Groups[1].Value.ToLowerInvariant();

                // Try to resolve the variable
                if (variableResolvers.TryGetValue(variableName, out var resolver))
                {
                    var value = resolver();
                    return value ?? string.Empty;
                }

                // Fail-safe: keep original variable if not found
                return match.Value;
            },
            RegexOptions.IgnoreCase
        );

        return result;
    }

    /// <summary>
    /// Gets all available variables with their descriptions
    /// </summary>
    public Dictionary<string, string> GetAvailableVariables()
    {
        return new Dictionary<string, string>
        {
            // Enfant (4 variables)
            { "%prenom_enfant%", "Prénom de l'enfant" },
            { "%nom_enfant%", "Nom de famille de l'enfant" },
            { "%nom_complet_enfant%", "Nom complet de l'enfant (prénom + nom)" },
            { "%date_naissance_enfant%", "Date de naissance de l'enfant (format: dd/MM/yyyy)" },

            // Parent (5 variables)
            { "%prenom_parent%", "Prénom du parent" },
            { "%nom_parent%", "Nom de famille du parent" },
            { "%nom_complet_parent%", "Nom complet du parent (prénom + nom)" },
            { "%email_parent%", "Adresse email du parent" },
            { "%telephone_parent%", "Numéro de téléphone du parent" },

            // Réservation (6 variables)
            { "%montant_total%", "Montant total de la réservation (€)" },
            { "%montant_paye%", "Montant déjà payé (€)" },
            { "%montant_restant%", "Montant restant à payer (€)" },
            { "%communication_structuree%", "Communication structurée pour le paiement" },
            { "%numero_reservation%", "Numéro de la réservation" },
            { "%groupe%", "Groupe assigné à l'enfant" },

            // Activité (4 variables)
            { "%nom_activite%", "Nom de l'activité" },
            { "%date_debut_activite%", "Date de début de l'activité (format: dd/MM/yyyy)" },
            { "%date_fin_activite%", "Date de fin de l'activité (format: dd/MM/yyyy)" },
            { "%prix_par_jour%", "Prix par jour de l'activité (€)" },

            // Organisation (3 variables)
            { "%nom_organisation%", "Nom de l'organisation" },
            { "%numero_compte%", "Numéro de compte bancaire de l'organisation" },
            { "%titulaire_compte%", "Nom du titulaire du compte bancaire" }
        };
    }

    /// <summary>
    /// Builds a dictionary of variable resolvers for the given booking
    /// </summary>
    private Dictionary<string, Func<string?>> BuildVariableResolvers(Booking booking, Organisation organisation)
    {
        return new Dictionary<string, Func<string?>>(StringComparer.OrdinalIgnoreCase)
        {
            // Enfant (4 variables)
            ["prenom_enfant"] = () => booking.Child?.FirstName,
            ["nom_enfant"] = () => booking.Child?.LastName,
            ["nom_complet_enfant"] = () => booking.Child != null
                ? $"{booking.Child.FirstName} {booking.Child.LastName}"
                : null,
            ["date_naissance_enfant"] = () => booking.Child?.BirthDate.ToString("dd/MM/yyyy"),

            // Parent (5 variables)
            ["prenom_parent"] = () => booking.Child?.Parent?.FirstName,
            ["nom_parent"] = () => booking.Child?.Parent?.LastName,
            ["nom_complet_parent"] = () => booking.Child?.Parent != null
                ? $"{booking.Child.Parent.FirstName} {booking.Child.Parent.LastName}"
                : null,
            ["email_parent"] = () => booking.Child?.Parent?.Email,
            ["telephone_parent"] = () => booking.Child?.Parent?.MobilePhoneNumber,

            // Réservation (6 variables)
            ["montant_total"] = () => booking.TotalAmount.ToString("F2"),
            ["montant_paye"] = () => booking.PaidAmount.ToString("F2"),
            ["montant_restant"] = () => (booking.TotalAmount - booking.PaidAmount).ToString("F2"),
            ["communication_structuree"] = () => booking.StructuredCommunication,
            ["numero_reservation"] = () => booking.Id.ToString(),
            ["groupe"] = () => booking.Group?.Label,

            // Activité (4 variables)
            ["nom_activite"] = () => booking.Activity?.Name,
            ["date_debut_activite"] = () => booking.Activity?.StartDate.ToString("dd/MM/yyyy"),
            ["date_fin_activite"] = () => booking.Activity?.EndDate.ToString("dd/MM/yyyy"),
            ["prix_par_jour"] = () => booking.Activity?.PricePerDay?.ToString("F2"),

            // Organisation (3 variables)
            ["nom_organisation"] = () => organisation.Name,
            ["numero_compte"] = () => organisation.BankAccountNumber,
            ["titulaire_compte"] = () => organisation.BankAccountName
        };
    }
}
