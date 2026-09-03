using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.Tests.Core.Auth;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_And_Verify_WithCorrectPassword_ReturnsTrue()
    {
        const string password = "SeniorCare-Test-2026!";

        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        const string password = "SeniorCare-Test-2026!";
        var hash = _hasher.Hash(password);

        var result = _hasher.Verify("Password-Incorrecta", hash);

        Assert.False(result);
    }

    [Fact]
    public void Verify_WithInvalidHash_ReturnsFalse()
    {
        Assert.False(_hasher.Verify("SeniorCare-Test-2026!", "hash-invalido"));
    }
}
