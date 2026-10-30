using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace SeniorCare.Api.App.Core.Auth;

/// <summary>
/// Impide arrancar con secretos de ejemplo o ya publicados. Una instalación nueva que copió
/// .env.example sin cambiarlo tendría claves conocidas: cualquiera podría fabricar tokens
/// o entrar con las contraseñas de demostración. Nunca incluye el valor en el mensaje.
/// </summary>
public static class InsecureSecretGuard
{
    public static readonly string[] SecretSettings =
    [
        "JWT_SECRET",
        "SEED_ADMIN_PASSWORD",
        "SEED_CAREGIVER_PASSWORD",
        "SEED_RESIDENT_PASSWORD",
        "SEED_FAMILY_PASSWORD"
    ];

    public static readonly string[] ConnectionStringNames =
    [
        "IdentityDatabase",
        "CareDatabase",
        "EmergencyDatabase",
        "AnalyticsDatabase"
    ];

    private static readonly string[] PlaceholderMarkers =
    [
        "CAMBIAR",
        "CHANGE_ME",
        "CHANGEME",
        "REEMPLAZAR"
    ];

    // SHA-256 de valores publicados en el repositorio (ejemplos antiguos y la configuración
    // de desarrollo de deploy/k8s). Se guardan como huella para no volver a publicarlos.
    private static readonly HashSet<string> KnownLeakedHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "859814b8f4a0c6b70f70843eeb694683e6539808b4cb90283c7bfc7c808e006c",
        "066e6948d62f9204b3d06e05c0820b4afd17fc5c77f47bd24f837e3deebe3ebe",
        "9036999e208fdfb79f9e193fa209c06490fa8f1661c3ad824d6e7dc662607b7f",
        "2baa2556378327d3b21b959da0ff5c74a453b765476baf3e3965d1e36750f870",
        "d13832997abd60c1021ec4437e1144ca41293974141a8ebc15ebb45a3e289e71",
        "26f8c13cdf7a3ffb6e0ba7701e529372bae51718c0fdca0926284b62f5d75071",
        "c07579c89b14be276ec97b95c717f04732a13354f4abb2e646c1e866540b1189",
        "0127947321d95f3cd165613633559173a850adafb114acf5ddee6c7a4902ca7b"
    };

    public static bool IsInsecure(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (PlaceholderMarkers.Any(marker =>
                trimmed.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)));
        return KnownLeakedHashes.Contains(hash);
    }

    /// <summary>Nombres (no valores) de las variables con secretos inseguros.</summary>
    public static IReadOnlyList<string> FindInsecureSettings(IConfiguration configuration)
    {
        var insecure = SecretSettings
            .Where(name => IsInsecure(configuration[name]))
            .ToList();

        foreach (var name in ConnectionStringNames)
        {
            var connectionString = configuration.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                continue;
            }

            string? password;
            try
            {
                password = new NpgsqlConnectionStringBuilder(connectionString).Password;
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (IsInsecure(password))
            {
                insecure.Add($"ConnectionStrings:{name} (POSTGRES_PASSWORD)");
            }
        }

        return insecure;
    }

    public static void EnsureSafe(IConfiguration configuration)
    {
        var insecure = FindInsecureSettings(configuration);
        if (insecure.Count > 0)
        {
            throw new InvalidOperationException(
                "SeniorCare OS no inicia con secretos de ejemplo o ya publicados en: "
                + string.Join(", ", insecure)
                + ". Generá valores nuevos con scripts/configurar-entorno.ps1 "
                + "(instalación nueva) o reemplazalos en .env.");
        }
    }
}
