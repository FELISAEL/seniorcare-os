using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SeniorCare.Api.Tests.Integration;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        const string variable = "JWT_SECRET";
        var previousValue = Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes");

        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                });

            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "healthy",
                body,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SeniorCare.Api", body);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }
}
