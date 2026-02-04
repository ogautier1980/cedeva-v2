using Cedeva.Core.Entities;

namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service de gestion des excursions
/// </summary>
public interface IExcursionService
{
    /// <summary>
    /// Récupère une excursion par ID
    /// </summary>
    Task<Excursion?> GetByIdAsync(int id);

    /// <summary>
    /// Récupère toutes les excursions d'une activité
    /// </summary>
    Task<List<Excursion>> GetByActivityIdAsync(int activityId);

    /// <summary>
    /// Inscrit un enfant (via son booking) à une excursion
    /// Met à jour Booking.TotalAmount et crée une transaction financière
    /// </summary>
    Task<ExcursionRegistration> RegisterChildAsync(int excursionId, int bookingId);

    /// <summary>
    /// Désinscrit un enfant d'une excursion
    /// Réduit Booking.TotalAmount et crée une transaction compensatoire
    /// </summary>
    Task<bool> UnregisterChildAsync(int excursionId, int bookingId);

    /// <summary>
    /// Récupère toutes les inscriptions d'une excursion
    /// </summary>
    Task<List<ExcursionRegistration>> GetRegistrationsAsync(int excursionId);

    /// <summary>
    /// Met à jour la présence d'un enfant à une excursion
    /// </summary>
    Task<bool> UpdateAttendanceAsync(int registrationId, bool isPresent);

    /// <summary>
    /// Récupère le résumé financier d'une excursion
    /// </summary>
    Task<ExcursionFinancialSummary> GetFinancialSummaryAsync(int excursionId);
}

/// <summary>
/// Résumé financier d'une excursion
/// </summary>
public class ExcursionFinancialSummary
{
    public decimal TotalRevenue { get; set; }  // Inscriptions × Coût
    public decimal TotalExpenses { get; set; }  // Somme des dépenses
    public decimal NetBalance { get; set; }     // Revenus - Dépenses
    public int RegistrationCount { get; set; }  // Nombre d'inscrits
}
