namespace Cedeva.Core.Entities;

/// <summary>
/// Transaction bancaire importée depuis un fichier CODA.
/// </summary>
public class BankTransaction : AuditableEntity
{
    public int Id { get; set; }

    public int OrganisationId { get; set; }
    public Organisation Organisation { get; set; } = null!;

    public DateTime TransactionDate { get; set; }

    public DateTime ValueDate { get; set; }

    /// <summary>
    /// Montant de la transaction (peut être négatif pour les débits)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Communication structurée (format +++XXX/XXXX/XXXXX+++)
    /// </summary>
    public string? StructuredCommunication { get; set; }

    /// <summary>
    /// Communication libre (texte non structuré)
    /// </summary>
    public string? FreeCommunication { get; set; }

    /// <summary>
    /// Nom de la contrepartie
    /// </summary>
    public string? CounterpartyName { get; set; }

    /// <summary>
    /// Compte bancaire de la contrepartie
    /// </summary>
    public string? CounterpartyAccount { get; set; }

    /// <summary>
    /// Code de transaction CODA (ex: "05" = virement, "01" = valeur mobilière, etc.)
    /// </summary>
    public string TransactionCode { get; set; } = null!;

    public int CodaFileId { get; set; }
    public CodaFile CodaFile { get; set; } = null!;

    /// <summary>
    /// Indique si cette transaction a été rapprochée avec une réservation
    /// </summary>
    public bool IsReconciled { get; set; }

    /// <summary>
    /// FK vers Payment si la transaction a été rapprochée
    /// </summary>
    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }
}
