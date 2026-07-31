namespace Cedeva.Core.Enums;

/// <summary>
/// Types of email templates
/// </summary>
public enum EmailTemplateType
{
    /// <summary>
    /// Confirmation d'inscription (verrouillé — unique par organisation, voir <see cref="EmailTemplateTypeExtensions.IsLocked"/>)
    /// </summary>
    BookingConfirmation = 1,

    /// <summary>
    /// Email de bienvenue nouvel utilisateur
    /// </summary>
    WelcomeEmail = 2,

    /// <summary>
    /// Rappel fiche médicale (verrouillé)
    /// </summary>
    MedicalSheetReminder = 3,

    /// <summary>
    /// Rappel paiement (verrouillé)
    /// </summary>
    PaymentReminder = 4,

    /// <summary>
    /// Annulation jour/activité
    /// </summary>
    ActivityCancellation = 5,

    /// <summary>
    /// Notification à l'organisation lors d'une nouvelle inscription
    /// </summary>
    NewRegistrationNotification = 6,

    /// <summary>
    /// Lien de paiement (+ QR code) envoyé au parent quand le coordinateur confirme une
    /// réservation avec un solde restant dû (verrouillé)
    /// </summary>
    PaymentLinkRequest = 7,

    /// <summary>
    /// Template personnalisé
    /// </summary>
    Custom = 99
}

public static class EmailTemplateTypeExtensions
{
    /// <summary>
    /// Types verrouillés : uniques au niveau organisation (jamais copiés/dupliqués par activité),
    /// modifiables mais ni supprimables ni duplicables. Décision produit du 2026-07-30 (Lot E).
    /// </summary>
    private static readonly HashSet<EmailTemplateType> LockedTypes =
    [
        EmailTemplateType.BookingConfirmation,
        EmailTemplateType.MedicalSheetReminder,
        EmailTemplateType.PaymentReminder,
        EmailTemplateType.PaymentLinkRequest
    ];

    public static bool IsLocked(this EmailTemplateType type) => LockedTypes.Contains(type);
}
