using System.Net;
using System.Net.Http.Json;
using Cedeva.Core.Entities;
using Cedeva.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cedeva.Tests.Integration;

/// <summary>
/// Drives the defensive catch branches of the ActivityQuestions JSON endpoints
/// <c>ToggleActive</c> and <c>UpdateOrder</c> by forcing persistence to fail
/// (<see cref="ThrowingSaveChangesInterceptor"/>). Both return a JSON error (HTTP 200, never 500)
/// from catch(InvalidOperationException)/catch(DbUpdateException)/catch(Exception); the change must
/// not persist.
/// </summary>
[Collection("WebApp")]
public class ActivityQuestionsErrorPathTests
{
    private sealed record Seeded(int OrgId, int Q1, int Q2);

    private static (CedevaWebApplicationFactory factory, Seeded seed) Seed()
    {
        var factory = new CedevaWebApplicationFactory();
        Organisation org = null!;
        ActivityQuestion q1 = null!, q2 = null!;
        factory.Seed(ctx =>
        {
            org = TestData.Organisation();
            var activity = TestData.Activity(org);
            q1 = TestData.Question(activity, "Q1", displayOrder: 0);
            q2 = TestData.Question(activity, "Q2", displayOrder: 1);
            ctx.AddRange(org, activity, q1, q2);
            return 0;
        });
        return (factory, new Seeded(org.Id, q1.Id, q2.Id));
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task ToggleActive_WhenSaveFails_ReturnsJsonError_AndUnchanged(string kind)
    {
        var (factory, s) = Seed();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
            factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

            var response = await client.PostAsync("/ActivityQuestions/ToggleActive", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = s.Q1.ToString(),
                ["isActive"] = "false",
            }));

            response.StatusCode.Should().Be(HttpStatusCode.OK, "the JSON endpoint catches the failure, never 500");

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.ActivityQuestions.SingleAsync(q => q.Id == s.Q1)).IsActive
                .Should().BeTrue("a failed toggle must not deactivate the question");
        }
    }

    [Theory]
    [MemberData(nameof(SaveFailures.Kinds), MemberType = typeof(SaveFailures))]
    public async Task UpdateOrder_WhenSaveFails_ReturnsJsonError_AndOrderUnchanged(string kind)
    {
        var (factory, s) = Seed();
        using (factory)
        {
            var client = factory.CreateClientFor("u1", s.OrgId, "Coordinator");
            factory.ThrowOnSaveChanges = SaveFailures.Make(kind);

            // Swap the two display orders.
            var response = await client.PostAsJsonAsync("/ActivityQuestions/UpdateOrder", new[]
            {
                new { Id = s.Q1, DisplayOrder = 1 },
                new { Id = s.Q2, DisplayOrder = 0 },
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            factory.ThrowOnSaveChanges = null;
            using var db = factory.NewDbContext();
            (await db.ActivityQuestions.SingleAsync(q => q.Id == s.Q1)).DisplayOrder
                .Should().Be(0, "a failed reorder must not persist");
        }
    }
}
