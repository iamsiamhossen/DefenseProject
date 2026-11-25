using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Infrastructure.Identity;
using KYAUCPChapter.Infrastructure.Persistence;

namespace KYAUCPChapter.Infrastructure.Tests;

[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTest(DatabaseFixture fixture) : IAsyncLifetime
{
    protected const int DurationMinutes = 300;

    protected static readonly DateTime Now = new(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime OpensAt = Now.AddDays(1);
    protected static readonly DateTime ClosesAt = Now.AddDays(2);
    protected static readonly DateTime StartsAt = Now.AddDays(3);
    protected static readonly DateTime FreezeAt = StartsAt.AddMinutes(240);
    protected static readonly DateTime DuringRegistration = OpensAt.AddHours(1);
    protected static readonly DateTime DuringContest = StartsAt.AddMinutes(30);
    protected static readonly DateTime AfterContest = StartsAt.AddMinutes(DurationMinutes + 1);

    protected DatabaseFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Contest NewContest(ContestMode mode = ContestMode.Individual, DateTime? freezeAt = null) =>
        Contest.Create("KYAU Monthly Contest", "Monthly practice contest", mode, new ContestSchedule(OpensAt, ClosesAt, StartsAt, DurationMinutes, freezeAt), Now);

    protected static Contest NewPublishedContest(ContestMode mode = ContestMode.Individual, DateTime? freezeAt = null)
    {
        var contest = NewContest(mode, freezeAt);
        contest.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3")], Now);
        contest.Publish(Now);
        return contest;
    }

    protected async Task<Contest> SaveAsync(Contest contest)
    {
        await using var db = Fixture.CreateContext();
        db.Contests.Add(contest);
        await db.SaveChangesAsync();
        return contest;
    }

    protected async Task<Guid> AddStudentAsync(string? studentId = null)
    {
        var id = Guid.CreateVersion7();
        var email = $"{id:N}@students.kyau.test";
        var studentNumber = studentId ?? id.ToString("N")[..12];

        await using var db = Fixture.CreateContext();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = "Test Student",
            StudentId = studentNumber,
            NormalizedStudentId = studentNumber.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
        return id;
    }

    protected static bool HasMessage(Exception exception, string text)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected ContestParticipationServiceScope Participation(DateTime now)
    {
        var db = Fixture.CreateContext();
        return new ContestParticipationServiceScope(db, Fixture.CreateParticipationService(db, now));
    }

    protected sealed class ContestParticipationServiceScope(AppDbContext db, ContestParticipationService service) : IAsyncDisposable
    {
        public ContestParticipationService Service { get; } = service;

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
