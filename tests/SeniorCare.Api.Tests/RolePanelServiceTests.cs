using SeniorCare.Api.App.Services.Identity;

namespace SeniorCare.Api.Tests.Services.Identity;

public sealed class RolePanelServiceTests
{
    private readonly RolePanelService _service = new();

    [Theory]
    [InlineData("admin", "/panel/administracion/")]
    [InlineData("caregiver", "/panel/cuidador/")]
    [InlineData("resident", "/panel/adulto-mayor/")]
    [InlineData("family", "/panel/familiar/")]
    public void Resolve_WithSupportedRole_ReturnsExpectedPanel(
        string role,
        string expectedPanel)
    {
        var result = _service.Resolve(role);

        Assert.Equal(expectedPanel, result);
    }

    [Fact]
    public void Resolve_WithUnsupportedRole_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => _service.Resolve("unknown"));
    }
}
