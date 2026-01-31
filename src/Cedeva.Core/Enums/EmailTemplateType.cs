namespace Cedeva.Core.Enums;

/// <summary>
/// Types of email templates
/// </summary>
public enum EmailTemplateType
{
    /// <summary>
    /// Confirmation de réservation
    /// </summary>
    BookingConfirmation = 1,

    /// <summary>
    /// Email de bienvenue nouvel utilisateur
    /// </summary>
    WelcomeEmail = 2,

    /// <summary>
    /// Rappel fiche médicale
    /// </summary>
    MedicalSheetReminder = 3,

    /// <summary>
    /// Rappel paiement
    /// </summary>
    PaymentReminder = 4,

    /// <summary>
    /// Annulation jour/activité
    /// </summary>
    ActivityCancellation = 5,

    /// <summary>
    /// Template personnalisé
    /// </summary>
    Custom = 99
}
