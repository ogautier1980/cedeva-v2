using System.Net;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Coverage for ActivityWizardController's birth-year quota management (Lot K #4) — Step4 GET/POST
/// and the AddBirthYearQuota/RemoveBirthYearQuota actions. The rest of the wizard (Steps 1-3, 5-7,
/// AddDate) has no automated test coverage yet — a pre-existing gap, out of scope here.
/// </summary>
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
            ctx.AddRange(org, activity);
            return 0;
        });
        return (org.Id, activity.Id);
    }

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
}
