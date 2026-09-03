using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SeniorCare.Api.Tests.Integration;

public sealed class AuthorizationEndpointTests
{
    [Fact]
    public async Task CareResidents_WithoutToken_ReturnsUnauthorized()
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

            var response = await client.GetAsync(
                "/api/care/residents");

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }
}
