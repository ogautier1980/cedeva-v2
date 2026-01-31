namespace Cedeva.Core.Interfaces;

/// <summary>
/// Service pour rapprocher les transactions bancaires avec les réservations.
/// Matching automatique par communication structurée et rapprochement manuel.
/// </summary>
public interface IBankReconciliationService
{
    /// <summary>
    /// Rapproche automatiquement les transactions d'un fichier CODA avec les réservations
    /// en utilisant la communication structurée.
    /// </summary>
    /// <param name="codaFileId">ID du fichier CODA</param>
    /// <returns>Nombre de transactions rapprochées</returns>
    Task<int> AutoReconcileTransactionsAsync(int codaFileId);

    /// <summary>
    /// Rapproche manuellement une transaction bancaire avec une réservation.
    /// </summary>
    /// <param name="transactionId">ID de la transaction bancaire</param>
    /// <param name="bookingId">ID de la réservation</param>
    /// <returns>True si réussi, false sinon</returns>
    Task<bool> ManualReconcileAsync(int transactionId, int bookingId);

    /// <summary>
    /// Récupère toutes les transactions non rapprochées pour une organisation.
    /// </summary>
    /// <param name="organisationId">ID de l'organisation</param>
    /// <returns>Liste des transactions non rapprochées</returns>
    Task<List<UnreconciledTransactionDto>> GetUnreconciledTransactionsAsync(int organisationId);

    /// <summary>
    /// Récupère toutes les réservations non ou partiellement payées pour une organisation.
    /// </summary>
    /// <param name="organisationId">ID de l'organisation</param>
    /// <returns>Liste des réservations en attente de paiement</returns>
    Task<List<UnpaidBookingDto>> GetUnpaidBookingsAsync(int organisationId);

    /// <summary>
    /// Suggère des rapprochements probables basés sur le montant et le nom du bénéficiaire.
    /// </summary>
    /// <param name="organisationId">ID de l'organisation</param>
    /// <returns>Liste des suggestions de rapprochement avec score de confiance</returns>
    Task<List<ReconciliationSuggestionDto>> GetReconciliationSuggestionsAsync(int organisationId);
}

/// <summary>
/// DTO pour une transaction bancaire non rapprochée
/// </summary>
public class UnreconciledTransactionDto
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? StructuredCommunication { get; set; }
    public string? FreeCommunication { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyAccount { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
}

/// <summary>
/// DTO pour une réservation non ou partiellement payée
/// </summary>
public class UnpaidBookingDto
{
    public int Id { get; set; }
    public string? StructuredCommunication { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public string ChildName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public DateTime ActivityStartDate { get; set; }
}

/// <summary>
/// DTO pour une suggestion de rapprochement semi-automatique
/// </summary>
public class ReconciliationSuggestionDto
{
    public int TransactionId { get; set; }
    public int BookingId { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal TransactionAmount { get; set; }
    public string? CounterpartyName { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public decimal BookingRemainingAmount { get; set; }
    public int ConfidenceScore { get; set; } // 0-100
    public List<string> MatchReasons { get; set; } = new();
}
