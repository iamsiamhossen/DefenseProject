using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace KYAUCPChapter.Infrastructure.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string TestDatabaseLockName = "kyau_cp_chapter_test_suite";
    private const int LockTimeoutSeconds = 600;

    private const string ResetSql = """
        DELETE FROM `FinalStandings`;
        DELETE FROM `Submissions`;
        DELETE FROM `ContestRegistrations`;
        DELETE FROM `TeamMembers`;
        DELETE FROM `Teams`;
        DELETE FROM `TestCases`;
        DELETE FROM `Problems`;
        DELETE FROM `Contests`;
        DELETE FROM `AspNetUserRoles`;
        DELETE FROM `AspNetUserClaims`;
        DELETE FROM `AspNetUserLogins`;
        DELETE FROM `AspNetUserTokens`;
        DELETE FROM `AspNetUsers`;
        """;

    private MySqlConnection? _lockConnection;

    public DatabaseFixture()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<DatabaseFixture>().Build();
        ConnectionString = configuration.GetConnectionString("Test")
            ?? throw new InvalidOperationException("Set the 'ConnectionStrings:Test' user secret to run the integration tests.");

        var database = new MySqlConnectionStringBuilder(ConnectionString).Database;
        if (!database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Integration tests only run against a database whose name ends with '_test', not '{database}'.");
        }
    }

    public string ConnectionString { get; }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseMySQL(ConnectionString).Options);

    public ContestParticipationService CreateParticipationService(AppDbContext db, DateTime now) =>
        new(db, new FixedTimeProvider(now));

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(ResetSql);
    }

    public async Task InitializeAsync()
    {
        _lockConnection = new MySqlConnection(ConnectionString);
        await _lockConnection.OpenAsync();
        await using (var command = new MySqlCommand("SELECT GET_LOCK(@name, @timeout)", _lockConnection))
        {
            command.Parameters.AddWithValue("@name", TestDatabaseLockName);
            command.Parameters.AddWithValue("@timeout", LockTimeoutSeconds);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            {
                throw new InvalidOperationException("Timed out waiting for exclusive use of the test database.");
            }
        }

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_lockConnection is not null)
        {
            await _lockConnection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
