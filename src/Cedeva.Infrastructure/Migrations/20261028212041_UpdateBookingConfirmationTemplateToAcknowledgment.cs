using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only: the organisation-level BookingConfirmation template (sent when a parent submits the
    /// registration form) becomes an acknowledgment — "request received, the amount to pay follows" —
    /// instead of a confirmation with the amount. Only templates still holding the original default
    /// text are rewritten; an organisation that customised its template keeps it.
    /// </summary>
    public partial class UpdateBookingConfirmationTemplateToAcknowledgment : Migration
    {
        private const string Greeting = "<p>Chère famille <strong>%nom_complet_parent%</strong>,</p>";

        private const string OldName = "Confirmation d'inscription";
        private const string OldSubject = "Confirmation inscription – %nom_activite%";
        private const string OldHtml =
            "<h2 style=\"color:#007faf;\">Confirmation de votre inscription</h2>" +
            Greeting +
            "<p>Nous vous confirmons l'inscription de <strong>%nom_complet_enfant%</strong> " +
            "à l'activité <strong>%nom_activite%</strong> " +
            "(du %date_debut_activite% au %date_fin_activite%).</p>" +
            "<p><strong>Montant total :</strong> %montant_total%<br>" +
            "<strong>Communication structurée :</strong> %communication_structuree%</p>" +
            "<p>En cas de question, n'hésitez pas à nous contacter.</p>" +
            "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>";

        private const string NewName = "Demande d'inscription reçue";
        private const string NewSubject = "Demande d'inscription reçue – %nom_activite%";
        private const string NewHtml =
            "<h2 style=\"color:#007faf;\">Demande d'inscription reçue</h2>" +
            Greeting +
            "<p>Nous avons bien reçu votre demande d'inscription de <strong>%nom_complet_enfant%</strong> " +
            "à l'activité <strong>%nom_activite%</strong> " +
            "(du %date_debut_activite% au %date_fin_activite%).</p>" +
            "<p>Notre équipe va la vérifier. Vous recevrez ensuite un e-mail avec le montant à payer.</p>" +
            "<p>En cas de question, n'hésitez pas à nous contacter.</p>" +
            "<p>Cordialement,<br><strong>%nom_organisation%</strong></p>";

        private const int BookingConfirmation = 1; // EmailTemplateType.BookingConfirmation

        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        private static string Rewrite(string fromName, string fromSubject, string fromHtml, string toName, string toSubject, string toHtml) =>
            $"UPDATE \"EmailTemplates\" SET \"Name\" = {Quote(toName)}, \"Subject\" = {Quote(toSubject)}, \"HtmlContent\" = {Quote(toHtml)} " +
            $"WHERE \"TemplateType\" = {BookingConfirmation} AND \"ActivityId\" IS NULL " +
            $"AND \"HtmlContent\" = {Quote(fromHtml)} AND \"Subject\" = {Quote(fromSubject)} AND \"Name\" = {Quote(fromName)};";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Rewrite(OldName, OldSubject, OldHtml, NewName, NewSubject, NewHtml));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Rewrite(NewName, NewSubject, NewHtml, OldName, OldSubject, OldHtml));
        }
    }
}
