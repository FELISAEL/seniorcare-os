using System.Security.Claims;
using SeniorCare.Api.App.Services.Care;

namespace SeniorCare.Api.Tests.Services.Care;

public sealed class ResidentAccessServiceTests
{
    private readonly ResidentAccessService _service = new();

    [Fact]
    public void CanRead_Admin_ReturnsTrue()
    {
        var principal = CreatePrincipal("admin");

        Assert.True(_service.CanRead(principal, Guid.NewGuid()));
    }

    [Fact]
    public void CanRead_ResidentWithMatchingId_ReturnsTrue()
    {
        var residentId = Guid.NewGuid();
        var principal = CreatePrincipal("resident", residentId);

        Assert.True(_service.CanRead(principal, residentId));
    }

    [Fact]
    public void CanRead_ResidentWithDifferentId_ReturnsFalse()
    {
        var principal = CreatePrincipal("resident", Guid.NewGuid());

        Assert.False(_service.CanRead(principal, Guid.NewGuid()));
    }

    [Fact]
    public void CanManageCare_Caregiver_ReturnsTrue()
    {
        var principal = CreatePrincipal("caregiver");

        Assert.True(_service.CanManageCare(principal, Guid.NewGuid()));
    }

    [Fact]
    public void CanManageCare_Family_ReturnsFalse()
    {
        var residentId = Guid.NewGuid();
        var principal = CreatePrincipal("family", residentId);

        Assert.False(_service.CanManageCare(principal, residentId));
    }

    [Fact]
    public void CanConfirmDose_ResidentWithMatchingId_ReturnsTrue()
    {
        var residentId = Guid.NewGuid();
        var principal = CreatePrincipal("resident", residentId);

        Assert.True(_service.CanConfirmDose(principal, residentId));
    }

    [Fact]
    public void CanConfirmDose_Family_ReturnsFalse()
    {
        var residentId = Guid.NewGuid();
        var principal = CreatePrincipal("family", residentId);

        Assert.False(_service.CanConfirmDose(principal, residentId));
    }

    private static ClaimsPrincipal CreatePrincipal(
        string role,
        Guid? residentId = null)
    {
        var claims = new List<Claim>
        {
            new("role", role)
        };

        if (residentId.HasValue)
        {
            claims.Add(
                new Claim(
                    "resident_id",
                    residentId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(
            claims,
            "Test",
            "name",
            "role");

        return new ClaimsPrincipal(identity);
    }
}
