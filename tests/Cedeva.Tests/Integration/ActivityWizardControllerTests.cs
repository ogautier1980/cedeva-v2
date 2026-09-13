using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Core.Enums;
using Cedeva.Core.Helpers;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>Coverage for all 7 steps of ActivityWizardController (Lot I).</summary>
[Collection("WebApp")]
public class ActivityWizardControllerTests
{
    private static (int OrgId, int ActivityId) SeedActivity(CedevaWebApplicationFactory factory)
    {
        Organisation org = null!;
        Activity activity = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            activity = TestData.Activity(org, "Stage Wizard Quota");
            ActivityDayGenerator.GenerateDays(activity);
            ctx.AddRange(org, activity);
            return 0;
        });
        return (org.Id, activity.Id);
    }

    // ------------------------------------------------------------------
    // Step 1
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step1_Get_RendersForm()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync("/ActivityWizard/Step1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Step1_Post_Valid_CreatesActivityAndRedirectsToStep2()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Stage de Pâques",
                ["StartDate"] = "2026-04-06",
                ["EndDate"] = "2026-04-10",
                ["OrganisationId"] = orgId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().StartWith("/ActivityWizard/Step2");

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Name == "Stage de Pâques");
        activity.OrganisationId.Should().Be(orgId);
        activity.StartDate.Should().Be(new DateTime(2026, 4, 6));
        activity.EndDate.Should().Be(new DateTime(2026, 4, 10));
        activity.IsActive.Should().BeTrue();
        (await ctx.ActivityDays.CountAsync(d => d.ActivityId == activity.Id)).Should().Be(5);
    }

    [Fact]
    public async Task Step1_Post_EndDateBeforeStartDate_ReturnsViewWithoutCreatingActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Stage Invalide",
                ["StartDate"] = "2026-04-10",
                ["EndDate"] = "2026-04-06",
                ["OrganisationId"] = orgId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().AnyAsync(a => a.Name == "Stage Invalide")).Should().BeFalse();
    }

    [Fact]
    public async Task Step1_Post_DuplicateNameInSameOrganisation_ReturnsViewWithoutCreatingActivity()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, _) = SeedActivity(factory); // seeds "Stage Wizard Quota" in orgId
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "  stage wizard quota  ", // same name, different case/whitespace
                ["StartDate"] = "2026-04-06",
                ["EndDate"] = "2026-04-10",
                ["OrganisationId"] = orgId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().CountAsync(a => a.OrganisationId == orgId)).Should().Be(1);
    }

    [Fact]
    public async Task Step1_Post_SameNameInDifferentOrganisation_IsAllowed()
    {
        using var factory = new CedevaWebApplicationFactory();
        SeedActivity(factory); // seeds "Stage Wizard Quota" in its own organisation
        var otherOrgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u2", otherOrgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Stage Wizard Quota",
                ["StartDate"] = "2026-04-06",
                ["EndDate"] = "2026-04-10",
                ["OrganisationId"] = otherOrgId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().AnyAsync(a => a.OrganisationId == otherOrgId && a.Name == "Stage Wizard Quota"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Step1_Post_WithId_RenamingToAnotherActivitysName_ReturnsViewWithoutUpdating()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory); // "Stage Wizard Quota"
        await using (var seedCtx = factory.NewDbContext())
        {
            var org = await seedCtx.Organisations.IgnoreQueryFilters().SingleAsync(o => o.Id == orgId);
            var other = TestData.Activity(org, "Autre Stage");
            seedCtx.Add(other);
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = activityId.ToString(),
                ["Name"] = "Autre Stage",
                ["StartDate"] = "2026-04-06",
                ["EndDate"] = "2026-04-10",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).Name.Should().Be("Stage Wizard Quota");
    }

    [Fact]
    public async Task Step1_Get_WithId_LoadsExistingActivityForEditing()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step1?id={activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stage Wizard Quota");
    }

    [Fact]
    public async Task Step1_Get_WithUnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync("/ActivityWizard/Step1?id=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Step1_Post_WithId_UpdatesExistingActivityInsteadOfCreatingNew()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        DateTime originalStart;
        await using (var seedCtx = factory.NewDbContext())
        {
            originalStart = (await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).StartDate;
        }
        var newEndDate = originalStart.AddDays(2); // shrink from 5 days (StartDate..+4) to 3 (StartDate..+2)

        var response = await client.PostAsync("/ActivityWizard/Step1", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Id"] = activityId.ToString(),
                ["Name"] = "Stage Wizard Renommé",
                ["StartDate"] = originalStart.ToString("yyyy-MM-dd"),
                ["EndDate"] = newEndDate.ToString("yyyy-MM-dd"),
                ["OrganisationId"] = orgId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be($"/ActivityWizard/Step2/{activityId}");

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().CountAsync()).Should().Be(1, "no new activity should be created");
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.Name.Should().Be("Stage Wizard Renommé");
        activity.StartDate.Should().Be(originalStart);
        activity.EndDate.Should().Be(newEndDate);
        // Date range shrunk from 5 to 3 days: the now out-of-range days are deactivated, not deleted.
        (await ctx.ActivityDays.CountAsync(d => d.ActivityId == activityId)).Should().Be(5);
        (await ctx.ActivityDays.CountAsync(d => d.ActivityId == activityId && d.IsActive)).Should().Be(3);
    }

    // ------------------------------------------------------------------
    // Step 2 / AddDate / Step2Next
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step2_Get_RendersGeneratedDaysGroupedByWeek()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step2/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Step2_Get_UnknownId_ReturnsNotFound()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync("/ActivityWizard/Step2/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddDate_BeforeCurrentStart_ExtendsRangeAndAddsDay()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        DateTime originalStart;
        await using (var seedCtx = factory.NewDbContext())
        {
            originalStart = (await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).StartDate;
        }
        var newDate = originalStart.AddDays(-1);

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/AddDate", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["dates"] = newDate.ToString("yyyy-MM-dd"),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step2");

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.StartDate.Should().Be(newDate.Date);
        (await ctx.ActivityDays.AnyAsync(d => d.ActivityId == activityId && d.DayDate == newDate.Date)).Should().BeTrue();
    }

    [Fact]
    public async Task AddDate_ExistingInactiveDay_Reactivates()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        DateTime targetDate;
        await using (var seedCtx = factory.NewDbContext())
        {
            var day = await seedCtx.ActivityDays.FirstAsync(d => d.ActivityId == activityId);
            day.IsActive = false;
            targetDate = day.DayDate;
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/AddDate", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["dates"] = targetDate.ToString("yyyy-MM-dd"),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        (await ctx.ActivityDays.SingleAsync(d => d.ActivityId == activityId && d.DayDate == targetDate)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddDate_MultipleDatesFromRangePicker_ExtendsRangeAndAddsAllOfThem()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        DateTime originalEnd;
        await using (var seedCtx = factory.NewDbContext())
        {
            originalEnd = (await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).EndDate;
        }
        var newDate1 = originalEnd.AddDays(1);
        var newDate2 = originalEnd.AddDays(2);

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/AddDate", new FormUrlEncodedContent(
            new[]
            {
                new KeyValuePair<string, string>("id", activityId.ToString()),
                new KeyValuePair<string, string>("dates", newDate1.ToString("yyyy-MM-dd")),
                new KeyValuePair<string, string>("dates", newDate2.ToString("yyyy-MM-dd")),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.EndDate.Should().Be(newDate2.Date);
        (await ctx.ActivityDays.AnyAsync(d => d.ActivityId == activityId && d.DayDate == newDate1.Date)).Should().BeTrue();
        (await ctx.ActivityDays.AnyAsync(d => d.ActivityId == activityId && d.DayDate == newDate2.Date)).Should().BeTrue();
    }

    [Fact]
    public async Task Step2Next_DeactivatesUnselectedDaysAndRedirectsToStep3()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        List<int> dayIds;
        await using (var seedCtx = factory.NewDbContext())
        {
            dayIds = await seedCtx.ActivityDays.Where(d => d.ActivityId == activityId).Select(d => d.DayId).ToListAsync();
        }
        var keptDayIds = dayIds.Take(dayIds.Count - 1).ToList(); // drop the last day

        var form = new List<KeyValuePair<string, string>> { new("id", activityId.ToString()) };
        form.AddRange(keptDayIds.Select(id => new KeyValuePair<string, string>("ActiveDayIds", id.ToString())));

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/Step2Next", new FormUrlEncodedContent(form));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step3");

        await using var ctx = factory.NewDbContext();
        var droppedDayId = dayIds.Except(keptDayIds).Single();
        (await ctx.ActivityDays.SingleAsync(d => d.DayId == droppedDayId)).IsActive.Should().BeFalse();
        foreach (var keptId in keptDayIds)
        {
            (await ctx.ActivityDays.SingleAsync(d => d.DayId == keptId)).IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Step2Next_AdvancesWizardMaxStepReachedTo3()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step2Next", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["id"] = activityId.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.WizardMaxStepReached.Should().Be(3);
    }

    // ------------------------------------------------------------------
    // Step 3 — Règlement
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step3_Get_RendersExistingRegulationFields()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            activity.RegulationLinkUrl = "https://example.be/roi.pdf";
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityWizard/Step3/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("https://example.be/roi.pdf");
    }

    [Fact]
    public async Task Step3_Get_WithNoAcceptanceTextYet_PrefillsLocalizedDefault()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step3/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("En inscrivant mon enfant");
    }

    [Fact]
    public async Task Step3_Post_SavesRegulationAndRedirectsToStep4()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step3", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["RegulationLinkUrl"] = "https://example.be/roi.pdf",
                ["RegulationAcceptanceText"] = "J'accepte le règlement d'ordre intérieur",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step4");

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.RegulationLinkUrl.Should().Be("https://example.be/roi.pdf");
        activity.RegulationAcceptanceText.Should().Be("J'accepte le règlement d'ordre intérieur");
    }

    [Fact]
    public async Task Step3_Post_BlankLinkAndNoFile_ReturnsViewWithoutSaving()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            activity.RegulationLinkUrl = "https://example.be/roi.pdf";
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/Step3", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["RegulationLinkUrl"] = "   ",
                ["RegulationAcceptanceText"] = "J'accepte",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).RegulationLinkUrl
            .Should().Be("https://example.be/roi.pdf");
    }

    [Fact]
    public async Task Step3_Post_MissingAcceptanceText_ReturnsViewWithoutSaving()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step3", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["RegulationLinkUrl"] = "https://example.be/roi.pdf",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).RegulationLinkUrl.Should().BeNull();
    }

    [Fact]
    public async Task Step3_Post_UploadedPdf_ReplacesTypedUrlAndStoresFile()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(activityId.ToString()), "ActivityId" },
            { new StringContent("https://example.be/roi-ignore.pdf"), "RegulationLinkUrl" },
            { new StringContent("J'accepte le règlement"), "RegulationAcceptanceText" }
        };
        var fileContent = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "RegulationPdfFile", "reglement.pdf");

        var response = await client.PostAsync("/ActivityWizard/Step3", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.RegulationLinkUrl.Should().NotBeNull();
        activity.RegulationLinkUrl.Should().NotBe("https://example.be/roi-ignore.pdf");
        activity.RegulationLinkUrl.Should().StartWith($"/uploads/activity-{activityId}-regulations/");
        activity.RegulationLinkUrl.Should().EndWith("reglement.pdf");
    }

    [Fact]
    public async Task Step3_Post_UploadedPdfWithWrongExtension_ReturnsViewWithoutSaving()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(activityId.ToString()), "ActivityId" },
            { new StringContent("J'accepte le règlement"), "RegulationAcceptanceText" }
        };
        var fileContent = new ByteArrayContent(new byte[] { 0x00, 0x01 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "RegulationPdfFile", "reglement.exe");

        var response = await client.PostAsync("/ActivityWizard/Step3", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).RegulationLinkUrl.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Step 4 — Limitations
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step4_Get_RendersExistingQuotas()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var ctx = factory.NewDbContext())
        {
            ctx.ActivityBirthYearQuotas.Add(new ActivityBirthYearQuota { ActivityId = activityId, BirthYear = 2018, MaxChildren = 10 });
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityWizard/Step4/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("2018");
    }

    [Fact]
    public async Task Step4_Post_SavesLimitationFieldsAndRedirectsToStep5()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step4", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["IncludedPostalCodes"] = "1000,1050",
                ["ExcludedPostalCodes"] = "2000",
                ["MaxChildrenPerDay"] = "30",
                ["FullMessage"] = "Stage complet",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step5");

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.IncludedPostalCodes.Should().Be("1000,1050");
        activity.ExcludedPostalCodes.Should().Be("2000");
        activity.MaxChildrenPerDay.Should().Be(30);
        activity.FullMessage.Should().Be("Stage complet");
    }

    [Fact]
    public async Task Step4_Post_InvalidRange_ReturnsViewWithQuotasReloaded()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            seedCtx.ActivityBirthYearQuotas.Add(new ActivityBirthYearQuota { ActivityId = activityId, BirthYear = 2017, MaxChildren = 5 });
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/Step4", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["MaxChildrenPerDay"] = "0", // below the [Range(1, 100000)] minimum
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("2017");

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).MaxChildrenPerDay.Should().BeNull();
    }

    [Fact]
    public async Task AddBirthYearQuota_NewYear_CreatesQuota()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/AddBirthYearQuota", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["birthYear"] = "2020",
                ["maxChildren"] = "24",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step4");

        await using var ctx = factory.NewDbContext();
        var quota = await ctx.ActivityBirthYearQuotas.SingleAsync(q => q.ActivityId == activityId && q.BirthYear == 2020);
        quota.MaxChildren.Should().Be(24);
    }

    [Fact]
    public async Task AddBirthYearQuota_ExistingYear_UpdatesMaxChildrenInstadOfDuplicating()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            seedCtx.ActivityBirthYearQuotas.Add(new ActivityBirthYearQuota { ActivityId = activityId, BirthYear = 2020, MaxChildren = 10 });
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/AddBirthYearQuota", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["birthYear"] = "2020",
                ["maxChildren"] = "24",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        ctx.ActivityBirthYearQuotas.Count(q => q.ActivityId == activityId && q.BirthYear == 2020).Should().Be(1);
        (await ctx.ActivityBirthYearQuotas.SingleAsync(q => q.ActivityId == activityId && q.BirthYear == 2020)).MaxChildren.Should().Be(24);
    }

    [Fact]
    public async Task AddBirthYearQuota_OutOfRangeYear_IsIgnored()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/AddBirthYearQuota", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["birthYear"] = "1800",
                ["maxChildren"] = "24",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        (await ctx.ActivityBirthYearQuotas.AnyAsync(q => q.ActivityId == activityId && q.BirthYear == 1800)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveBirthYearQuota_RemovesRow()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        int quotaId;
        await using (var seedCtx = factory.NewDbContext())
        {
            var quota = new ActivityBirthYearQuota { ActivityId = activityId, BirthYear = 2019, MaxChildren = 15 };
            seedCtx.ActivityBirthYearQuotas.Add(quota);
            await seedCtx.SaveChangesAsync();
            quotaId = quota.Id;
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/RemoveBirthYearQuota", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["quotaId"] = quotaId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        (await ctx.ActivityBirthYearQuotas.AnyAsync(q => q.Id == quotaId)).Should().BeFalse();
    }

    [Fact]
    public async Task Step4_Post_SavesBirthYearQuotaExceededMessage()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step4", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["BirthYearQuotaExceededMessage"] = "Complet pour cette année de naissance",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step5");

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).BirthYearQuotaExceededMessage
            .Should().Be("Complet pour cette année de naissance");
    }

    // ------------------------------------------------------------------
    // Step 5 — Autres questions
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step5_Get_RendersExistingQuestions()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            seedCtx.ActivityQuestions.Add(TestData.Question(activity, "Allergies connues ?"));
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityWizard/Step5/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Allergies connues ?");
    }

    [Fact]
    public async Task Step5_Get_NewQuestionTemplateDoesNotEscapeIndexInterpolation()
    {
        // Regression guard: the "Ajouter une question" JS template literal previously escaped the
        // `$` in `${newQuestionIndex}` (i.e. `\${newQuestionIndex}`), which meant every dynamically
        // added row was named e.g. "NewQuestions[${newQuestionIndex}].QuestionText" literally
        // instead of "NewQuestions[0].QuestionText" — the model binder silently ignored these
        // unrecognized indices, so new questions were never saved.
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step5/{activityId}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain("\\${newQuestionIndex}");
        html.Should().Contain("NewQuestions[${idx}].QuestionText");
    }

    [Fact]
    public async Task Step5_Get_ShowsOptionsFieldForCheckboxQuestionType()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            var question = TestData.Question(activity, "Allergies ?");
            question.QuestionType = QuestionType.Checkbox;
            question.Options = "Gluten,Arachides";
            seedCtx.ActivityQuestions.Add(question);
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityWizard/Step5/{activityId}");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Gluten,Arachides");
        var match = System.Text.RegularExpressions.Regex.Match(
            html, "id=\"existing_options_0\"\\s+style=\"display:\\s*(\\w+);\"");
        match.Success.Should().BeTrue("the options container for the existing question should carry a display style");
        match.Groups[1].Value.Should().Be("block");
        html.Should().Contain("selectedType === '1' || selectedType === '2' || selectedType === '3'");
    }

    [Fact]
    public async Task Step5_Post_AddsNewQuestionAndRedirectsToStep6()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step5", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["NewQuestions[0].QuestionText"] = "Numéro de sécurité sociale ?",
                ["NewQuestions[0].QuestionType"] = ((int)QuestionType.Text).ToString(),
                ["NewQuestions[0].IsRequired"] = "true",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/ActivityWizard/Step6");

        await using var ctx = factory.NewDbContext();
        var question = await ctx.ActivityQuestions.SingleAsync(q => q.ActivityId == activityId);
        question.QuestionText.Should().Be("Numéro de sécurité sociale ?");
        question.IsRequired.Should().BeTrue();
        question.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Step5_Post_UpdatesExistingQuestion()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        int questionId;
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            var question = TestData.Question(activity, "Ancien texte");
            seedCtx.ActivityQuestions.Add(question);
            await seedCtx.SaveChangesAsync();
            questionId = question.Id;
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/Step5", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["ExistingQuestions[0].Id"] = questionId.ToString(),
                ["ExistingQuestions[0].QuestionText"] = "Texte modifié",
                ["ExistingQuestions[0].QuestionType"] = ((int)QuestionType.Text).ToString(),
                ["ExistingQuestions[0].IsActive"] = "true",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        (await ctx.ActivityQuestions.SingleAsync(q => q.Id == questionId)).QuestionText.Should().Be("Texte modifié");
    }

    [Fact]
    public async Task RemoveQuestion_DeletesActivityQuestionAndRedirectsToStep5()
    {
        // Covers removing a question copied by default from an organisation question template
        // (Lot J) from this one activity, without touching the template itself.
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        int questionId;
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            var question = TestData.Question(activity, "Allergies connues ?");
            seedCtx.ActivityQuestions.Add(question);
            await seedCtx.SaveChangesAsync();
            questionId = question.Id;
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/RemoveQuestion", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["questionId"] = questionId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain($"/ActivityWizard/Step5/{activityId}");

        await using var ctx = factory.NewDbContext();
        (await ctx.ActivityQuestions.AnyAsync(q => q.Id == questionId)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveQuestion_QuestionBelongsToAnotherActivity_DoesNotDeleteIt()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var (_, otherActivityId) = SeedActivity(factory);
        int questionId;
        await using (var seedCtx = factory.NewDbContext())
        {
            var otherActivity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == otherActivityId);
            var question = TestData.Question(otherActivity, "Question d'une autre activité");
            seedCtx.ActivityQuestions.Add(question);
            await seedCtx.SaveChangesAsync();
            questionId = question.Id;
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.PostAsync("/ActivityWizard/RemoveQuestion", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = activityId.ToString(),
                ["questionId"] = questionId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        await using var ctx = factory.NewDbContext();
        (await ctx.ActivityQuestions.AnyAsync(q => q.Id == questionId)).Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Step 6 — Affichage
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step6_Get_RendersPublicationFields()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step6/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Step6_Post_Valid_RedirectsToEmbedCode()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step6", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["PublicationStartDate"] = "2026-03-01",
                ["PublicationEndDate"] = "2026-03-31",
                ["NoActiveFormMessage"] = "Inscriptions bientôt ouvertes",
                ["RedirectUrlAfterSubmit"] = "https://example.be/merci",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/PublicRegistration/EmbedCode");
        response.Headers.Location!.ToString().Should().Contain(activityId.ToString());

        await using var ctx = factory.NewDbContext();
        var activity = await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
        activity.PublicationStartDate.Should().Be(new DateTime(2026, 3, 1));
        activity.PublicationEndDate.Should().Be(new DateTime(2026, 3, 31));
        activity.NoActiveFormMessage.Should().Be("Inscriptions bientôt ouvertes");
        activity.RedirectUrlAfterSubmit.Should().Be("https://example.be/merci");
    }

    [Fact]
    public async Task Step6_Post_MissingRequiredFields_ReturnsViewWithoutSaving()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step6", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).PublicationStartDate.Should().BeNull();
    }

    [Fact]
    public async Task Step6_Post_EndBeforeStart_ReturnsViewWithoutSaving()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.PostAsync("/ActivityWizard/Step6", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["ActivityId"] = activityId.ToString(),
                ["PublicationStartDate"] = "2026-03-31",
                ["PublicationEndDate"] = "2026-03-01",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ctx = factory.NewDbContext();
        (await ctx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId)).PublicationStartDate.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Navigation: "Précédent" buttons + clickable progress-gauge circles
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step2_Get_RendersPreviousButtonLinkingToStep1WithId()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step2/{activityId}");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain($"/ActivityWizard/Step1/{activityId}");
    }

    [Fact]
    public async Task Step6_Get_ProgressGauge_HasClickableLinksToAllPreviousSteps()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync($"/ActivityWizard/Step6/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        for (var step = 1; step <= 5; step++)
        {
            html.Should().Contain($"/ActivityWizard/Step{step}/{activityId}",
                $"step {step} is already completed and should be clickable from Step6's progress gauge");
        }
    }

    [Fact]
    public async Task Step2_Get_ProgressGauge_HasClickableLinksToAlreadyReachedFollowingSteps()
    {
        using var factory = new CedevaWebApplicationFactory();
        var (orgId, activityId) = SeedActivity(factory);
        await using (var seedCtx = factory.NewDbContext())
        {
            var activity = await seedCtx.Activities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityId);
            activity.WizardMaxStepReached = 4;
            await seedCtx.SaveChangesAsync();
        }

        var client = factory.CreateClientFor("u1", orgId, "Coordinator");
        var response = await client.GetAsync($"/ActivityWizard/Step2/{activityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain($"/ActivityWizard/Step3/{activityId}",
            "step 3 was already reached before navigating back to step 2, so it should stay clickable");
        html.Should().Contain($"/ActivityWizard/Step4/{activityId}",
            "step 4 was already reached before navigating back to step 2, so it should stay clickable");
        html.Should().NotContain($"/ActivityWizard/Step5/{activityId}",
            "step 5 was never reached, so it should not be clickable");
    }

    [Fact]
    public async Task Step1_Get_ProgressGauge_HasNoClickableSteps_SinceItIsTheFirstOne()
    {
        using var factory = new CedevaWebApplicationFactory();
        var orgId = factory.Seed(ctx =>
        {
            var org = TestData.Organisation();
            ctx.Add(org);
            return org;
        }).Id;
        var client = factory.CreateClientFor("u1", orgId, "Coordinator");

        var response = await client.GetAsync("/ActivityWizard/Step1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("wizard-progress-circle rounded-circle d-flex align-items-center justify-content-center bg-primary text-white text-decoration-none",
            "step 1 is the current step, not a completed one, so no circle should be a clickable link");
    }
}
