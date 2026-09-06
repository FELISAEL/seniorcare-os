using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeniorCare.Api.App.Core.Auth;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Repositories.Identity;

namespace SeniorCare.Api.Tests.Integration;

[Collection("Integration")]
public sealed class LoginEndpointTests
{
    private const string JwtSecret =
        "SeniorCare-Integration-Test-Secret-Key-2026-32-Bytes";

    private const string TestPassword =
        "SeniorCare-Test-Password-2026!";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        var repository = new FakeIdentityRepository(
            CreateAdmin());

        using var response = await SendLoginAsync(
            repository,
            new LoginRequest("admin", TestPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(login);
        Assert.Equal("admin", login!.User.Username);
        Assert.Equal("admin", login.User.Role);
        Assert.Equal(
            "/panel/administracion/",
            login.PanelPath);
        Assert.False(
            string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(repository.UpdateLastLoginCalled);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var repository = new FakeIdentityRepository(
            CreateAdmin());

        using var response = await SendLoginAsync(
            repository,
            new LoginRequest(
                "admin",
                "Password-Incorrecta"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.False(repository.UpdateLastLoginCalled);
    }

    [Fact]
    public async Task Login_ThenMe_WithReturnedToken_ReturnsCurrentUser()
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            var repository = new FakeIdentityRepository(
                CreateAdmin());

            await using var factory =
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment("Testing");

                        builder.ConfigureServices(services =>
                        {
                            services.RemoveAll<IIdentityRepository>();

                            services.AddSingleton<IIdentityRepository>(
                                repository);
                        });
                    });

            using var client = factory.CreateClient();

            using var loginResponse = await client.PostAsJsonAsync(
                "/api/identity/login",
                new LoginRequest("admin", TestPassword));

            Assert.Equal(
                HttpStatusCode.OK,
                loginResponse.StatusCode);

            var login =
                await loginResponse.Content
                    .ReadFromJsonAsync<LoginResponse>();

            Assert.NotNull(login);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    login!.AccessToken);

            using var meResponse = await client.GetAsync(
                "/api/identity/me");

            Assert.Equal(
                HttpStatusCode.OK,
                meResponse.StatusCode);

            var json = JsonDocument.Parse(
                await meResponse.Content.ReadAsStringAsync());

            var root = json.RootElement;

            Assert.Equal(
                "admin",
                root.GetProperty("username").GetString());

            Assert.Equal(
                "admin",
                root.GetProperty("role").GetString());

            Assert.Equal(
                "/panel/administracion/",
                root.GetProperty("panelPath").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }

    [Fact]
    public async Task Login_WhenRateLimited_Returns429WithJsonBody()
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            var repository = new FakeIdentityRepository(
                CreateAdmin());

            await using var factory =
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment("Testing");

                        builder.ConfigureServices(services =>
                        {
                            services.RemoveAll<IIdentityRepository>();

                            services.AddSingleton<IIdentityRepository>(
                                repository);
                        });
                    });

            using var client = factory.CreateClient();

            var acceptedCount = 0;
            HttpResponseMessage? rejected = null;

            for (var attempt = 0; attempt < 11; attempt++)
            {
                var response = await client.PostAsJsonAsync(
                    "/api/identity/login",
                    new LoginRequest("admin", TestPassword));

                if (response.StatusCode
                    == HttpStatusCode.TooManyRequests)
                {
                    rejected = response;
                    break;
                }

                Assert.Equal(
                    HttpStatusCode.OK,
                    response.StatusCode);

                acceptedCount++;
                response.Dispose();
            }

            Assert.Equal(10, acceptedCount);
            Assert.NotNull(rejected);

            using (rejected)
            {
                Assert.Equal(
                    HttpStatusCode.TooManyRequests,
                    rejected!.StatusCode);

                Assert.Equal(
                    "application/json",
                    rejected.Content.Headers.ContentType?.MediaType);

                var body =
                    await rejected.Content.ReadAsStringAsync();

                Assert.False(
                    string.IsNullOrWhiteSpace(body));

                using var json = JsonDocument.Parse(body);

                Assert.Equal(
                    "Demasiados intentos. Esperá un minuto e intentá nuevamente.",
                    json.RootElement
                        .GetProperty("message")
                        .GetString());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(
        FakeIdentityRepository repository,
        LoginRequest request)
    {
        const string variable = "JWT_SECRET";
        var previousValue =
            Environment.GetEnvironmentVariable(variable);

        Environment.SetEnvironmentVariable(
            variable,
            JwtSecret);

        try
        {
            await using var factory =
                new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment("Testing");

                        builder.ConfigureServices(services =>
                        {
                            services.RemoveAll<IIdentityRepository>();

                            services.AddSingleton<IIdentityRepository>(
                                repository);
                        });
                    });

            using var client = factory.CreateClient();

            return await client.PostAsJsonAsync(
                "/api/identity/login",
                request);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                variable,
                previousValue);
        }
    }

    private static UserAccount CreateAdmin()
    {
        var hasher = new PasswordHasher();

        return new UserAccount(
            Guid.NewGuid(),
            "admin",
            "Administrador SeniorCare",
            hasher.Hash(TestPassword),
            SeniorCareRoles.Admin,
            null,
            true,
            DateTimeOffset.UtcNow,
            null);
    }

    private sealed class FakeIdentityRepository(
        UserAccount user) : IIdentityRepository
    {
        public bool UpdateLastLoginCalled { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<UserAccount?> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(
                string.Equals(
                    username,
                    user.Username,
                    StringComparison.OrdinalIgnoreCase)
                    ? user
                    : null);

        public Task<IReadOnlyList<PublicUser>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PublicUser>>(
                Array.Empty<PublicUser>());

        public Task<PublicUser> CreateAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset> UpdateLastLoginAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            UpdateLastLoginCalled = true;

            return Task.FromResult(
                DateTimeOffset.UtcNow);
        }
    }
}

