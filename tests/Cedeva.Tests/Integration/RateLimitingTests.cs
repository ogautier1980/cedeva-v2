using System.Net;
using Cedeva.Tests.TestSupport;

namespace Cedeva.Tests.Integration;

[Collection("WebApp")]
public class RateLimitingTests
{
    [Fact]
    public async Task PublicRegistration_ReturnsTooManyRequests_AfterExceedingLimit()
    {
        using var factory = new CedevaWebApplicationFactory(); // fresh limiter state per test
        factory.Seed(_ => 0);
        var client = factory.CreateClient();

        // Policy "public-registration" allows 30 requests/minute per IP.
        HttpStatusCode? sawThrottled = null;
        for (var i = 0; i < 40; i++)
        {
            var response = await client.GetAsync("/PublicRegistration/SelectActivity?orgId=1");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                sawThrottled = response.StatusCode;
                break;
            }
        }

        sawThrottled.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
