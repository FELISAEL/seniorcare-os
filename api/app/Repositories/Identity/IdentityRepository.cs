using Npgsql;
using NpgsqlTypes;
using SeniorCare.Api.App.Models.Identity;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Repositories.Identity;

public sealed class IdentityRepository : IIdentityRepository, IAsyncDisposable
{
    private static readonly Guid DemoResidentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly NpgsqlDataSource _dataSource;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher _passwordHasher;

    public IdentityRepository(
        IConfiguration configuration,
        PasswordHasher passwordHasher)
    {
        _configuration = configuration;
        _passwordHasher = passwordHasher;

        var connectionString =
            configuration.GetConnectionString("IdentityDatabase")
            ?? throw new InvalidOperationException(
                "No se configuró ConnectionStrings:IdentityDatabase.");

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            CREATE TABLE IF NOT EXISTS users (
                id uuid PRIMARY KEY,
                username varchar(80) NOT NULL UNIQUE,
                display_name varchar(160) NOT NULL,
                password_hash text NOT NULL,
                role varchar(40) NOT NULL,
                resident_id uuid NULL,
                is_active boolean NOT NULL DEFAULT true,
                created_at timestamptz NOT NULL DEFAULT now(),
                last_login_at timestamptz NULL
            );

            ALTER TABLE users
                ADD COLUMN IF NOT EXISTS resident_id uuid NULL;

            ALTER TABLE users
                ADD COLUMN IF NOT EXISTS last_login_at timestamptz NULL;
            """;

        await using (var command = _dataSource.CreateCommand(sql))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await SeedUserAsync(
            _configuration["SEED_ADMIN_USERNAME"] ?? "admin",
            "Administrador SeniorCare",
            RequireSetting("SEED_ADMIN_PASSWORD"),
            "admin",
            null,
            cancellationToken);

        await SeedUserAsync(
            _configuration["SEED_CAREGIVER_USERNAME"] ?? "cuidador",
            "Cuidador de demostración",
            RequireSetting("SEED_CAREGIVER_PASSWORD"),
            "caregiver",
            null,
            cancellationToken);

        await SeedUserAsync(
            _configuration["SEED_RESIDENT_USERNAME"] ?? "maria",
            "María López",
            RequireSetting("SEED_RESIDENT_PASSWORD"),
            "resident",
            DemoResidentId,
            cancellationToken);

        await SeedUserAsync(
            _configuration["SEED_FAMILY_USERNAME"] ?? "ana",
            "Ana López",
            RequireSetting("SEED_FAMILY_PASSWORD"),
            "family",
            DemoResidentId,
            cancellationToken);
    }

    public async Task<UserAccount?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id,
                   username,
                   display_name,
                   password_hash,
                   role,
                   resident_id,
                   is_active,
                   created_at,
                   last_login_at
            FROM users
            WHERE lower(username) = lower($1)
            LIMIT 1;
            """;

        await using var command =
            _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(
            username.Trim());

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadUser(reader)
            : null;
    }

    public async Task<IReadOnlyList<PublicUser>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            SELECT id,
                   username,
                   display_name,
                   password_hash,
                   role,
                   resident_id,
                   is_active,
                   created_at,
                   last_login_at
            FROM users
            ORDER BY display_name;
            """;

        var users = new List<PublicUser>();

        await using var command =
            _dataSource.CreateCommand(sql);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(
                TokenService.ToPublic(
                    ReadUser(reader)));
        }

        return users;
    }

    public async Task<PublicUser> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = new UserAccount(
            Guid.NewGuid(),
            request.Username.Trim().ToLowerInvariant(),
            request.DisplayName.Trim(),
            _passwordHasher.Hash(request.Password),
            request.Role.Trim().ToLowerInvariant(),
            request.ResidentId,
            true,
            DateTimeOffset.UtcNow,
            null);

        const string sql =
            """
            INSERT INTO users (
                id,
                username,
                display_name,
                password_hash,
                role,
                resident_id,
                is_active,
                created_at,
                last_login_at
            )
            VALUES (
                @id,
                @username,
                @display_name,
                @password_hash,
                @role,
                @resident_id,
                @is_active,
                @created_at,
                NULL
            );
            """;

        await using var command =
            _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(
            "id",
            user.Id);

        command.Parameters.AddWithValue(
            "username",
            user.Username);

        command.Parameters.AddWithValue(
            "display_name",
            user.DisplayName);

        command.Parameters.AddWithValue(
            "password_hash",
            user.PasswordHash);

        command.Parameters.AddWithValue(
            "role",
            user.Role);

        var residentIdParameter =
            command.Parameters.Add(
                "resident_id",
                NpgsqlDbType.Uuid);

        residentIdParameter.Value =
            user.ResidentId.HasValue
                ? user.ResidentId.Value
                : DBNull.Value;

        command.Parameters.AddWithValue(
            "is_active",
            user.IsActive);

        command.Parameters.AddWithValue(
            "created_at",
            user.CreatedAt);

        await command.ExecuteNonQueryAsync(
            cancellationToken);

        return TokenService.ToPublic(user);
    }

    public async Task<DateTimeOffset> UpdateLastLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            UPDATE users
            SET last_login_at = now()
            WHERE id = $1
            RETURNING last_login_at;
            """;

        await using var command =
            _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(userId);

        var value =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return value is DateTimeOffset timestamp
            ? timestamp
            : DateTimeOffset.UtcNow;
    }

    private string RequireSetting(string key)
    {
        var value = _configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"No se configuró la variable requerida {key}.");
        }

        return value;
    }

    private async Task SeedUserAsync(
        string username,
        string displayName,
        string password,
        string role,
        Guid? residentId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO users (
                id,
                username,
                display_name,
                password_hash,
                role,
                resident_id,
                is_active,
                created_at,
                last_login_at
            )
            VALUES (
                @id,
                @username,
                @display_name,
                @password_hash,
                @role,
                @resident_id,
                true,
                now(),
                NULL
            )
            ON CONFLICT (username)
            DO UPDATE SET
                resident_id = COALESCE(
                    users.resident_id,
                    EXCLUDED.resident_id
                );
            """;

        await using var command =
            _dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue(
            "id",
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "username",
            username.Trim().ToLowerInvariant());

        command.Parameters.AddWithValue(
            "display_name",
            displayName);

        command.Parameters.AddWithValue(
            "password_hash",
            _passwordHasher.Hash(password));

        command.Parameters.AddWithValue(
            "role",
            role);

        var residentIdParameter =
            command.Parameters.Add(
                "resident_id",
                NpgsqlDbType.Uuid);

        residentIdParameter.Value =
            residentId.HasValue
                ? residentId.Value
                : DBNull.Value;

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static UserAccount ReadUser(
        NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5)
                ? null
                : reader.GetGuid(5),
            reader.GetBoolean(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(8));

    public ValueTask DisposeAsync() =>
        _dataSource.DisposeAsync();
}