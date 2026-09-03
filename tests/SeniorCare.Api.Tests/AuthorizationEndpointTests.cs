using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class AuthorizationEndpointTests
{
    [Theory]
    [InlineData("/api/care/residents")]
    [InlineData("/api/emergencies/alerts/active")]
    [InlineData("/api/communication/video-rooms/active")]
    [InlineData("/api/analytics/recent?days=7")]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized(
        string endpoint)
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

            var response = await client.GetAsync(endpoint);

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

