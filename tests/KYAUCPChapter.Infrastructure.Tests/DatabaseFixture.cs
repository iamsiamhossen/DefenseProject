using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace KYAUCPChapter.Infrastructure.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
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
        DELETE FROM `AspNetRoleClaims`;
        DELETE FROM `AspNetRoles`;
        """;

    private readonly string _connectionString;

    public DatabaseFixture()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<DatabaseFixture>().Build();
        _connectionString = configuration.GetConnectionString("Test")
            ?? throw new InvalidOperationException("Set the 'ConnectionStrings:Test' user secret to run the integration tests.");

        var database = new MySqlConnectionStringBuilder(_connectionString).Database;
        if (!database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Integration tests only run against a database whose name ends with '_test', not '{database}'.");
        }
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseMySQL(_connectionString).Options);

    public ContestParticipationService CreateParticipationService(AppDbContext db, DateTime now) =>
        new(db, new FixedTimeProvider(now));

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(ResetSql);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
