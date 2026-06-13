using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Services;
using NSubstitute;

namespace Cedeva.Tests.Services;

public class FacadeServicesTests
{
    // ---------------------------------------------------------------------
    // EmailFacadeService
    // ---------------------------------------------------------------------

    [Fact]
    public void EmailFacade_ExposesInjectedServices()
    {
        var email = Substitute.For<IEmailService>();
        var recipient = Substitute.For<IEmailRecipientService>();
        var variableReplacement = Substitute.For<IEmailVariableReplacementService>();
        var template = Substitute.For<IEmailTemplateService>();

        var sut = new EmailFacadeService(email, recipient, variableReplacement, template);

        sut.Email.Should().BeSameAs(email);
        sut.Recipient.Should().BeSameAs(recipient);
        sut.VariableReplacement.Should().BeSameAs(variableReplacement);
        sut.Template.Should().BeSameAs(template);
    }

    [Fact]
    public void EmailFacade_NullEmail_Throws()
    {
        var act = () => new EmailFacadeService(
            null!,
            Substitute.For<IEmailRecipientService>(),
            Substitute.For<IEmailVariableReplacementService>(),
            Substitute.For<IEmailTemplateService>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("email");
    }

    [Fact]
    public void EmailFacade_NullRecipient_Throws()
    {
        var act = () => new EmailFacadeService(
            Substitute.For<IEmailService>(),
            null!,
            Substitute.For<IEmailVariableReplacementService>(),
            Substitute.For<IEmailTemplateService>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("recipient");
    }

    [Fact]
    public void EmailFacade_NullVariableReplacement_Throws()
    {
        var act = () => new EmailFacadeService(
            Substitute.For<IEmailService>(),
            Substitute.For<IEmailRecipientService>(),
            null!,
            Substitute.For<IEmailTemplateService>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("variableReplacement");
    }

    [Fact]
    public void EmailFacade_NullTemplate_Throws()
    {
        var act = () => new EmailFacadeService(
            Substitute.For<IEmailService>(),
            Substitute.For<IEmailRecipientService>(),
            Substitute.For<IEmailVariableReplacementService>(),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("template");
    }

    [Fact]
    public void EmailFacade_ImplementsInterface()
    {
        IEmailFacadeService sut = new EmailFacadeService(
            Substitute.For<IEmailService>(),
            Substitute.For<IEmailRecipientService>(),
            Substitute.For<IEmailVariableReplacementService>(),
            Substitute.For<IEmailTemplateService>());

        sut.Should().BeAssignableTo<IEmailFacadeService>();
    }

    // ---------------------------------------------------------------------
    // ExportFacadeService
    // ---------------------------------------------------------------------

    [Fact]
    public void ExportFacade_ExposesInjectedServices()
    {
        var excel = Substitute.For<IExcelExportService>();
        var pdf = Substitute.For<IPdfExportService>();

        var sut = new ExportFacadeService(excel, pdf);

        sut.Excel.Should().BeSameAs(excel);
        sut.Pdf.Should().BeSameAs(pdf);
    }

    [Fact]
    public void ExportFacade_NullExcel_Throws()
    {
        var act = () => new ExportFacadeService(null!, Substitute.For<IPdfExportService>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("excel");
    }

    [Fact]
    public void ExportFacade_NullPdf_Throws()
    {
        var act = () => new ExportFacadeService(Substitute.For<IExcelExportService>(), null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdf");
    }

    [Fact]
    public void ExportFacade_ImplementsInterface()
    {
        IExportFacadeService sut = new ExportFacadeService(
            Substitute.For<IExcelExportService>(),
            Substitute.For<IPdfExportService>());

        sut.Should().BeAssignableTo<IExportFacadeService>();
    }
}
