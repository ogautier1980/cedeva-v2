using System.Globalization;
using Cedeva.Core.DTOs;
using Cedeva.Core.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cedeva.Infrastructure.Services.Pdf;

/// <summary>
/// Renders the fiscal attestation from the client's model (form 281.86, sent 19/11/2026): page 1
/// letter to the parents, page 2 official explanations, page 3 Cadre I, then one Cadre II page per
/// child. The texts are the official French ones of the model; only dates, amounts, organisation,
/// parent and children change.
/// </summary>
public static class FiscalAttestationPdf
{
    private static readonly CultureInfo FrBe = new("fr-BE");

    static FiscalAttestationPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(FiscalAttestationData data)
    {
        return Document.Create(document =>
        {
            Page(document, page => Letter(page, data));
            Page(document, Explanations);
            Page(document, page => CadreOne(page, data));
            // The form has room for 4 periods: beyond that, further attestations for the same child
            // (SPF guide § 3.4), each with its own order number.
            foreach (var child in data.Children)
            {
                foreach (var (orderNumber, periods) in SplitAttestations(data, child))
                {
                    Page(document, page => CadreTwo(page, data, child, orderNumber, periods));
                }
            }
        }).GeneratePdf();
    }

    private static void Page(IDocumentContainer document, Action<IContainer> content)
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(45);
            page.DefaultTextStyle(text => text.FontSize(10.5f));
            page.Content().Element(content);
        });
    }

    // Slashes as on the official form ("28/07/2025"): fr-BE would turn "/" into its own "-".
    private const int MaxPeriodsPerAttestation = 4;

    /// <summary>
    /// Order number, mandatory on every attestation (SPF guide § 3.1): year-parent-child, plus a
    /// suffix when a child needs several attestations. Stable, so a reprint carries the same number.
    /// </summary>
    public static string OrderNumber(FiscalAttestationData data, FiscalAttestationChild child, int? part = null) =>
        $"{data.Year}-{data.ParentId}-{child.ChildId}" + (part.HasValue ? $"-{part}" : string.Empty);

    /// <summary>A child's attestations: 4 periods each at most, numbered "-1", "-2"… when there are several.</summary>
    public static IReadOnlyList<(string OrderNumber, IReadOnlyList<FiscalAttestationPeriod> Periods)> SplitAttestations(
        FiscalAttestationData data, FiscalAttestationChild child)
    {
        var chunks = child.Periods.Chunk(MaxPeriodsPerAttestation).ToList();
        return chunks
            .Select((periods, i) => (OrderNumber(data, child, chunks.Count > 1 ? i + 1 : null), (IReadOnlyList<FiscalAttestationPeriod>)periods))
            .ToList();
    }

    /// <summary>
    /// Note (6) and SPF guide « Tarif journalier »: the daily rate (price / days) is only stated when it
    /// exceeds the daily ceiling — but as soon as one rate of the attestation does, all are stated.
    /// </summary>
    public static bool ShowDailyRates(decimal? maxDailyAmount, IEnumerable<FiscalAttestationPeriod> periods) =>
        maxDailyAmount.HasValue && periods.Any(p => DailyRate(p) > maxDailyAmount.Value);

    private static decimal DailyRate(FiscalAttestationPeriod period) => period.Days > 0 ? period.Amount / period.Days : 0;

    private static string Nrn(string nationalRegisterNumber) =>
        string.IsNullOrWhiteSpace(nationalRegisterNumber) ? string.Empty : NationalRegisterNumberHelper.Format(nationalRegisterNumber);

    private static string Date(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Euro(decimal amount) => amount.ToString("N2", FrBe) + " €";

    // ---- Page 1: letter to the parents ------------------------------------------------------

    private static void Letter(IContainer container, FiscalAttestationData data)
    {
        var year = data.Year;
        var ceiling = data.MaxDailyAmount.HasValue ? Euro(data.MaxDailyAmount.Value) : "…";
        var code = string.IsNullOrEmpty(data.TaxCode) ? "…" : data.TaxCode;

        container.Column(column =>
        {
            column.Spacing(12);
            column.Item().PaddingTop(40).AlignRight().Text($"Le {Date(data.IssuedDate)}");
            column.Item().PaddingTop(30).Text("Chers parents,");
            column.Item().Text($"Durant l'année {year}, votre enfant a été accueilli par nos soins. Dans le cadre de cet accueil, vous trouverez, en annexe, l'attestation en matière de frais de garde d'enfants âgés de moins de 14 ans (ou de moins de 21 ans en cas de handicap lourd).").Justify();
            column.Item().Text($"A la seconde page de cette attestation, sont repris les montants que vous nous avez payés durant l'année {year}.").Justify();
            column.Item().Text("En aucun cas, cette attestation n'est une facture. Les montants qui y figurent ayant déjà été payés à l'opérateur d'accueil par vos soins, aucune somme n'est donc due à l'O.N.E.").Justify();
            column.Item().Text($"Ces frais de garde peuvent, par contre, faire l'objet d'une réduction d'impôts. Vous pouvez faire valoir maximum {ceiling} par jour de garde et par enfant de moins de 14 ans (ou de moins de 21 ans en cas de handicap lourd) pour l'année {year} (exercice d'imposition {year + 1}). La réduction d'impôt s'élève à 45 % de ces dépenses.").Justify();
            column.Item().Text($"Vous devez mentionner ces dépenses (en tenant compte de la limite) au cadre X, rubrique B, code {code} de votre déclaration d'impôt. La réduction d'impôt (45 %) est calculée automatiquement.").Justify();
            column.Item().Text("Nous vous en souhaitons bonne réception et vous prions de croire, Madame, Monsieur, à l'expression de notre considération distinguée.").Justify();
            column.Item().PaddingTop(10).Text("Pour toute question, n'hésitez pas à nous contacter.");

            var organisation = data.Organisation;
            column.Item().PaddingTop(30).AlignRight().Width(220).Column(signature =>
            {
                if (organisation.Logo != null)
                {
                    signature.Item().AlignRight().Height(70).Image(organisation.Logo).FitArea();
                }
                signature.Item().AlignRight().Text(organisation.Name).Bold().FontSize(13);
                signature.Item().AlignRight().Text(organisation.Street);
                signature.Item().AlignRight().Text(organisation.PostalCodeCity);
                if (!string.IsNullOrEmpty(organisation.Phone))
                {
                    signature.Item().AlignRight().Text(organisation.Phone);
                }
            });
        });
    }

    // ---- Page 2: official explanations ------------------------------------------------------

    private static readonly (string Number, string Text)[] ExplanationNotes =
    [
        ("(1)", "Cette attestation, qui ne doit être complétée qu'en un seul exemplaire, doit être tenue à la disposition de l'administration par le débiteur des frais de garde d'enfant. Le débiteur des frais est la personne à qui incombe normalement les frais de garde et qui les paie ou les supporte. Le débiteur mentionné dans l'attestation aura seulement droit à la réduction d'impôt s'il a fiscalement les enfants à sa charge ou s'il reçoit la moitié de l'avantage fiscal pour enfants à charge (coparentalité fiscale), et si bien sûr les autres conditions légales sont respectées."),
        ("(2)", "Il s'agit :\n- de l'institution, du milieu d'accueil, de la crèche, de la famille d'accueil, de l'école, établi(e) dans l'Espace économique européen,\n- du pouvoir organisateur de l'école établie dans l'Espace économique européen,\n- du pouvoir public local, communautaire ou régional,\n- de l'institution publique étrangère établie dans un autre Etat membre de l'Espace économique européen,\n- de l'organisation établie dans l'Espace économique européen qui organise une garde à domicile pour des enfants malades par des gardiens professionnels,\n- du (de la) gardien(ne) indépendant(e) qui garde un enfant malade dans le cadre de son activité professionnelle qu'il (elle) exerce au sein de l'Espace économique européen."),
        ("(3)", "À compléter quand l'organisme qui assure la garde n'est autorisé(e), agréé(e), subsidié(e), etc … que pendant une partie de l'année pour laquelle l'attestation est établie."),
        ("(4)", "Il s'agit :\n- soit de l'Office de la Naissance et de l'Enfance ou de « Kind & Gezin » / « Opgroeien regie » ou du gouvernement de la Communauté germanophone ;\n- soit des pouvoirs publics locaux, communautaires ou régionaux ;\n- soit des institutions publiques étrangères établies dans un autre État membre de l'Espace économique européen ;\n- soit de l'école établie dans l'Espace économique européen ou du pouvoir organisateur de l'école établie dans l'Espace économique européen avec laquelle (lequel) les institutions ou les milieux d'accueil ont un lien."),
        ("(5)", "Les données mentionnées sur l'attestation ne peuvent concerner que la partie de l'année précédant le 14ème anniversaire de l'enfant ou le 21ème anniversaire de l'enfant avec un handicap lourd."),
        ("(6)", "Si plusieurs tarifs sont appliqués, il convient de fournir le détail du nombre total de jours de garde par tarif appliqué. Le tarif journalier ne doit cependant être mentionné que s'il est supérieur au montant maximum par jour de garde. Ce montant est indexé."),
        ("(7)", "S'il y a un mandataire, sa signature doit être précédée de la mention \"par procuration\".\nLa signature n'est pas obligatoire pour l'envoi électronique d'une copie à l'administration par l'application Belcotax on-web, l'utilisateur devant s'enregistrer pour utiliser celle-ci.")
    ];

    private static void Explanations(IContainer container)
    {
        container.DefaultTextStyle(text => text.FontSize(9)).Column(column =>
        {
            column.Spacing(9);
            column.Item().Text("Explications").Bold();
            foreach (var (number, text) in ExplanationNotes)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(28).Text(number);
                    row.RelativeItem().Text(text).Justify();
                });
            }
        });
    }

    // ---- Page 3: Cadre I --------------------------------------------------------------------

    private static void CadreOne(IContainer container, FiscalAttestationData data)
    {
        var organisation = data.Organisation;
        container.Border(1).Column(column =>
        {
            column.Item().BorderBottom(1).Padding(10).Column(header =>
            {
                header.Item().AlignCenter().Text($"ATTESTATION N°281.86 (ANNEE DES DEPENSES {data.Year})").Bold().Underline();
                header.Item().PaddingTop(6).AlignCenter().Text(text =>
                {
                    text.AlignCenter();
                    text.Span("Cette attestation vaut comme attestation annuelle délivrée conformément à l'art. 63").Bold();
                    text.Span("18/8").Bold().Superscript();
                    text.Span(", de l'arrêté royal d'exécution du Code des impôts sur les revenus 1992 (AR/CIR 92), en vue de l'octroi de la réduction d'impôt pour garde d'enfant (1).").Bold();
                });
            });

            column.Item().Padding(12).Column(body =>
            {
                body.Spacing(8);
                body.Item().Text(text =>
                {
                    text.Span("Nom de l'organisme ou de la personne (ci-après, « l'organisme ») ");
                    text.Span("qui assure la garde").Bold();
                    text.Span(" (2) :");
                });
                body.Item().PaddingLeft(15).Column(organisationLines =>
                {
                    organisationLines.Item().Text(organisation.Name);
                    if (!string.IsNullOrWhiteSpace(organisation.CompanyNumber))
                    {
                        organisationLines.Item().Text($"N°BCE : {organisation.CompanyNumber}");
                    }
                    foreach (var activityName in organisation.ActivityNames)
                    {
                        organisationLines.Item().Text(activityName);
                    }
                    organisationLines.Item().Text(organisation.Street);
                    organisationLines.Item().Text(organisation.PostalCodeCity);
                });

                body.Item().PaddingTop(10).Text(text =>
                {
                    text.Span("Cadre I").Bold().Underline();
                    text.Span(" (ce cadre I ne doit pas être complété dans tous les cas – voir avis)");
                });
                body.Item().Text("L'organisme qui assure la garde, certifie qu'il (cochez la case adéquate) :");
                Checkbox(body, true, "est autorisé, agréé, subsidié, contrôlé ou surveillé ou a reçu un label de qualité par l'Office de la Naissance et de l'Enfance ou par « Kind & Gezin » / « Opgroeien regie » ou par le gouvernement de la Communauté germanophone ;");
                Checkbox(body, false, "est autorisé, agréé, subsidié, contrôlé par les pouvoirs publics locaux, communautaires ou régionaux ;");
                Checkbox(body, false, "est autorisé, agréé, subsidié, contrôlé ou surveillé par des institutions publiques étrangères établies dans un autre état membre de l'Espace économique européen ;");
                Checkbox(body, false, "a un lien avec une école établie dans l'Espace économique européen ou le pouvoir organisateur d'une école établie dans l'Espace économique européen,");
                body.Item().Text(text =>
                {
                    text.Span("en application de l'article 145");
                    text.Span("35").Superscript();
                    text.Span($", al. 2, 3°, du Code des impôts sur les revenus 1992. Ce qui est certifié ci-dessus est valable pour la période du 01/01/{data.Year} au 31/12/{data.Year} (3).");
                });

                body.Item().PaddingTop(10).Text("Nom et adresse complète de « l'organisme certificateur » (4) qui a autorisé, agréé, subsidié, accordé un label de qualité ou qui contrôle ou surveille l'organisme de garde ou qui a un lien avec l'organisme de garde dans le cas des écoles ou de leurs pouvoirs organisateurs :");
                body.Item().PaddingLeft(15).Column(certifier =>
                {
                    certifier.Spacing(3);
                    certifier.Item().Text("Nom : Office de la Naissance et de l'Enfance");
                    certifier.Item().Text("N°BCE (facultatif) : ……………………………………");
                    certifier.Item().Text("Rue : Chaussée de Charleroi            N° : 95");
                    certifier.Item().Text("Code postal : 1060            Commune : Bruxelles");
                });
            });
        });
    }

    private static void Checkbox(ColumnDescriptor column, bool isChecked, string text)
    {
        column.Item().Row(row =>
        {
            row.ConstantItem(16).PaddingTop(2).Width(9).Height(9).Border(1)
                .Background(isChecked ? Colors.Black : Colors.White);
            row.RelativeItem().Text(text);
        });
    }

    // ---- Cadre II: one page per child -------------------------------------------------------

    private static void CadreTwo(IContainer container, FiscalAttestationData data, FiscalAttestationChild child,
        string orderNumber, IReadOnlyList<FiscalAttestationPeriod> periods)
    {
        container.Border(1).Padding(14).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("Cadre II").Bold().Underline();
            column.Item().Text(text =>
            {
                text.Span("1. Numéro d'ordre de l'attestation : ");
                text.Span(orderNumber).Bold();
            });
            column.Item().Text("2. Coordonnées du débiteur des frais de garde d'enfant :");
            column.Item().PaddingLeft(220).Column(parent =>
            {
                parent.Item().Text(data.ParentFullName).Bold();
                parent.Item().Text(data.ParentStreet);
                parent.Item().Text(data.ParentPostalCodeCity);
            });
            // The label is long: the number goes on its own line rather than wrapping after a lone colon.
            column.Item().PaddingLeft(15).Column(nrn =>
            {
                nrn.Item().Text("Numéro d'identification du Registre national ou, le cas échéant, le numéro d'identification de la BCSS :");
                nrn.Item().Text(Nrn(data.ParentNationalRegisterNumber)).Bold();
            });

            column.Item().PaddingTop(6).Text("3. Coordonnées de l'enfant :");
            column.Item().PaddingLeft(15).Column(childLines =>
            {
                childLines.Spacing(3);
                childLines.Item().Text($"{child.LastName} {child.FirstName}").Bold();
                childLines.Item().Text("Numéro d'identification du Registre national ou, le cas échéant, le numéro d'identification de la BCSS :");
                childLines.Item().Text(Nrn(child.NationalRegisterNumber)).Bold();
                childLines.Item().Text($"Date de naissance : {Date(child.BirthDate)}");
                childLines.Item().Text($"Adresse : {child.Address}");
            });

            column.Item().PaddingTop(6).Text("4. Période pendant laquelle l'enfant a été gardé (5) :");
            column.Item().Table(table => PeriodsTable(table, data, periods));

            column.Item().PaddingTop(8).Text("Le soussigné certifie exacts les renseignements mentionnés ci-avant.");
            column.Item().PaddingLeft(260).Column(signature =>
            {
                signature.Spacing(4);
                signature.Item().Text($"Fait à {data.Organisation.City}, le {Date(data.IssuedDate)}");
                signature.Item().Text(text =>
                {
                    text.Span("Personne habilitée à représenter ");
                    text.Span("l'organisme de garde ou").Bold();
                    text.Span(" représentant ");
                    text.Span("la personne qui assure la garde").Bold();
                    text.Span(" (2) (7).");
                });
                signature.Item().Text($"Nom : {data.Organisation.ResponsibleName}");
                signature.Item().Text($"Qualité : {data.Organisation.ResponsibleTitle}");
                signature.Item().Text("Signature :");
                if (data.Organisation.Signature != null)
                {
                    signature.Item().Height(60).AlignLeft().Image(data.Organisation.Signature).FitArea();
                }
            });
        });
    }

    private static void PeriodsTable(TableDescriptor table, FiscalAttestationData data, IReadOnlyList<FiscalAttestationPeriod> periods)
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(1.1f);
            columns.RelativeColumn(2.2f);
            columns.RelativeColumn(1f);
            columns.RelativeColumn(1f);
            columns.RelativeColumn(1.1f);
        });

        static IContainer Cell(IContainer cell) => cell.Border(1).Padding(4);

        table.Header(header =>
        {
            header.Cell().Element(Cell).Text("Période").Bold();
            header.Cell().Element(Cell).Text("Du jj/mm/aaaa au jj/mm/aaaa").Bold();
            header.Cell().Element(Cell).Text("Nombre de jours").Bold();
            header.Cell().Element(Cell).Text(text =>
            {
                text.Span("Tarif journalier").Bold();
                text.Span(" (6)");
            });
            header.Cell().Element(Cell).Text("Montant perçu").Bold();
        });

        var showRates = ShowDailyRates(data.MaxDailyAmount, periods);

        var number = 1;
        foreach (var period in periods)
        {
            table.Cell().Element(Cell).Text($"Période {number++}").Bold();
            table.Cell().Element(Cell).Text($"Du {Date(period.From)} au {Date(period.To)}");
            table.Cell().Element(Cell).Text(period.Days.ToString(FrBe));
            table.Cell().Element(Cell).Text(showRates ? Euro(Math.Round(DailyRate(period), 2)) : string.Empty);
            table.Cell().Element(Cell).Text(Euro(period.Amount));
        }

        table.Cell().ColumnSpan(4).Element(Cell).Text("Total").Bold();
        table.Cell().Element(Cell).Text(Euro(periods.Sum(p => p.Amount)));
    }
}
