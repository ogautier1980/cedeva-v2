using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Infrastructure.Services.Email;

/// <summary>
/// The default organisation-level email template library. Every organisation gets these templates so
/// that template-driven sending (booking confirmation, reminders, …) always has a default to use —
/// which is what lets the app drop the old hard-coded HTML fallbacks.
/// </summary>
public static class DefaultEmailTemplateLibrary
{
    private const string GreetingParent = "<p>Chère famille <strong>%nom_complet_parent%</strong>,</p>";

    /// <summary>Builds the default organisation-level templates (ActivityId = null) for an org.</summary>
    public static List<EmailTemplate> Build(int organisationId) => new()
    {
        new EmailTemplate
        {
            OrganisationId = organisationId,
            Name = "Confirmation d'inscription",
            TemplateType = EmailTemplateType.BookingConfirmation,
            Subject = "Confirmation inscription – %nom_activite%",
            HtmlContent =
                "<h2 style=\"color:#007faf;\">Confirmation de votre inscription</h2>" +
                GreetingParent +
                "<p>Nous vous confirmons l'inscription de <strong>%nom_complet_enfant%</strong> " +
                "à l'activité <strong>%nom_activite%</strong> " +
                "(du %date_debut_activite% au %date_fin_activite%).</p>" +
                "<p><strong>Montant total :</strong> %montant_total%<br>" +
                "<strong>Communication structurée :</strong> %communication_structuree%</p>" +
                "<p>En cas de question, n'hésitez pas à nous contacter.</p>" +
                "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>",
            IsDefault = true
        },
        new EmailTemplate
        {
            OrganisationId = organisationId,
            Name = "Rappel de paiement",
            TemplateType = EmailTemplateType.PaymentReminder,
            Subject = "Rappel paiement – %nom_complet_enfant% – %nom_activite%",
            HtmlContent =
                "<h2 style=\"color:#dc3545;\">Rappel de paiement</h2>" +
                GreetingParent +
                "<p>Le paiement pour l'inscription de <strong>%nom_complet_enfant%</strong> " +
                "à <strong>%nom_activite%</strong> est en attente.</p>" +
                "<p><strong>Montant restant :</strong> %montant_restant%<br>" +
                "<strong>Communication structurée :</strong> %communication_structuree%</p>" +
                "<p>Merci de procéder au paiement par virement bancaire dans les meilleurs délais.</p>" +
                "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>",
            IsDefault = true
        },
        new EmailTemplate
        {
            OrganisationId = organisationId,
            Name = "Rappel fiche médicale",
            TemplateType = EmailTemplateType.MedicalSheetReminder,
            Subject = "Fiche médicale manquante – %nom_complet_enfant%",
            HtmlContent =
                "<h2 style=\"color:#ffc107;\">Fiche médicale à compléter</h2>" +
                GreetingParent +
                "<p>La fiche médicale de <strong>%nom_complet_enfant%</strong> " +
                "pour l'activité <strong>%nom_activite%</strong> n'a pas encore été reçue.</p>" +
                "<p>Merci de nous la transmettre au plus tôt pour que votre enfant puisse " +
                "bénéficier de tous les services proposés.</p>" +
                "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>",
            IsDefault = true
        },
        new EmailTemplate
        {
            OrganisationId = organisationId,
            Name = "Message de bienvenue",
            TemplateType = EmailTemplateType.Custom,
            Subject = "Bienvenue chez %nom_organisation% !",
            HtmlContent =
                "<h2 style=\"color:#28a745;\">Bienvenue !</h2>" +
                GreetingParent +
                "<p>Nous sommes ravis de vous accueillir parmi nous. " +
                "<strong>%nom_complet_enfant%</strong> va passer un merveilleux séjour " +
                "à <strong>%nom_activite%</strong> !</p>" +
                "<p>N'hésitez pas à nous contacter si vous avez des questions.</p>" +
                "<p>À bientôt !<br><strong>L'équipe de %nom_organisation%</strong></p>",
            IsDefault = false
        },
        new EmailTemplate
        {
            OrganisationId = organisationId,
            Name = "Notification nouvelle inscription",
            TemplateType = EmailTemplateType.NewRegistrationNotification,
            Subject = "Nouvelle inscription – %nom_complet_enfant% – %nom_activite%",
            HtmlContent =
                "<h2 style=\"color:#007faf;\">Nouvelle inscription</h2>" +
                "<p>Une nouvelle inscription vient d'être enregistrée :</p>" +
                "<ul>" +
                "<li><strong>Enfant :</strong> %nom_complet_enfant%</li>" +
                "<li><strong>Parent :</strong> %nom_complet_parent% (%email_parent%, %telephone_parent%)</li>" +
                "<li><strong>Activité :</strong> %nom_activite%</li>" +
                "<li><strong>Réservation n° :</strong> %numero_reservation%</li>" +
                "</ul>" +
                "<p>Connectez-vous à Cedeva pour la traiter.</p>",
            IsDefault = true
        }
    };

    /// <summary>
    /// Adds the default library to an organisation if it has none yet (organisation-level templates).
    /// Idempotent. Returns the number of templates created.
    /// </summary>
    public static async Task<int> EnsureAsync(CedevaDbContext context, int organisationId, CancellationToken ct = default)
    {
        var hasLibrary = await context.EmailTemplates.IgnoreQueryFilters()
            .AnyAsync(t => t.OrganisationId == organisationId && t.ActivityId == null, ct);
        if (hasLibrary)
            return 0;

        var templates = Build(organisationId);
        context.EmailTemplates.AddRange(templates);
        await context.SaveChangesAsync(ct);
        return templates.Count;
    }
}
