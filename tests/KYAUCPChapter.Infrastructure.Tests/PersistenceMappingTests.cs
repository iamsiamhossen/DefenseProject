using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Scoring;
using KYAUCPChapter.Core.Domain.Submissions;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Tests;

public sealed class PersistenceMappingTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Contest_aggregate_round_trips_with_utc_schedule_problems_and_test_cases()
    {
        var contest = NewContest(ContestMode.Team, FreezeAt);
        contest.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3"), new("2 2", "4")], Now);
        contest.AddTophProblem("B", "Add Them Up", "add-them-up", "https://toph.co/p/add-them-up", Now);
        contest.Publish(Now);
        await SaveAsync(contest);

        await using var db = Fixture.CreateContext();
        var loaded = await db.Contests.Include(c => c.Problems).ThenInclude(p => p.TestCases).SingleAsync(c => c.Id == contest.Id);

        Assert.Equal(contest.Schedule, loaded.Schedule);
        Assert.Equal(DateTimeKind.Utc, loaded.Schedule.StartsAt.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.Schedule.FreezeAt!.Value.Kind);
        Assert.Equal((ContestMode.Team, ContestStatus.Published, "KYAU Monthly Contest"), (loaded.Mode, loaded.Status, loaded.Title));

        var internalProblem = loaded.Problems.Single(p => p.Label == "A");
        Assert.Equal((ProblemType.Internal, 1000, 262144), (internalProblem.Type, internalProblem.TimeLimitMs, internalProblem.MemoryLimitKb));
        Assert.Equal([(1, "1 2", "3"), (2, "2 2", "4")], internalProblem.TestCases.OrderBy(t => t.Position).Select(t => (t.Position, t.Input, t.ExpectedOutput)));

        var toph = loaded.Problems.Single(p => p.Label == "B");
        Assert.Equal((ProblemType.Toph, "add-them-up", "https://toph.co/p/add-them-up"), (toph.Type, toph.ExternalProblemId, toph.ExternalProblemUrl));
        Assert.Empty(toph.TestCases);
    }

    [Fact]
    public async Task Changes_to_a_loaded_contest_aggregate_are_persisted()
    {
        var contest = NewContest();
        var problem = contest.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3"), new("2 2", "4")], Now);
        contest.Publish(Now);
        await SaveAsync(contest);

        await using (var db = Fixture.CreateContext())
        {
            var tracked = await db.Contests.Include(c => c.Problems).ThenInclude(p => p.TestCases).SingleAsync(c => c.Id == contest.Id);
            tracked.UpdateDetails("Renamed Contest", "Updated", DuringRegistration);
            tracked.RemoveTestCase(problem.Id, 1, DuringRegistration);
            tracked.AddTestCase(problem.Id, "5 5", "10", isSample: false, DuringRegistration);
            tracked.AddInternalProblem("B", "Product", "Print a * b.", 2000, 131072, [new("2 3", "6")], DuringRegistration);
            await db.SaveChangesAsync();
        }

        await using var verify = Fixture.CreateContext();
        var loaded = await verify.Contests.Include(c => c.Problems).ThenInclude(p => p.TestCases).SingleAsync(c => c.Id == contest.Id);
        Assert.Equal("Renamed Contest", loaded.Title);
        Assert.Equal([2, 3], loaded.Problems.Single(p => p.Label == "A").TestCases.Select(t => t.Position).Order());
        Assert.Equal("6", loaded.Problems.Single(p => p.Label == "B").TestCases.Single().ExpectedOutput);
    }

    [Fact]
    public async Task Non_utc_timestamps_are_rejected_on_save()
    {
        var contest = await SaveAsync(NewPublishedContest());
        var student = await AddStudentAsync();
        var registration = ContestRegistration.ForStudent(contest, student, DuringRegistration);
        var localTime = DateTime.SpecifyKind(DuringContest, DateTimeKind.Local);
        var submission = Submission.Create(contest, contest.Problems.Single(), registration, null, student, "C++17", "int main() {}", localTime);

        await using var db = Fixture.CreateContext();
        db.ContestRegistrations.Add(registration);
        db.Submissions.Add(submission);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        Assert.True(HasMessage(error, "Only UTC timestamps can be stored"));
    }

    [Fact]
    public async Task Team_members_and_team_registration_round_trip()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var leader = await AddStudentAsync();
        var member = await AddStudentAsync();

        Guid teamId;
        string inviteCode;
        await using (var scope = Participation(DuringRegistration))
        {
            var team = await scope.Service.CreateTeamAsync(contest.Id, "Null Pointers", leader, CancellationToken.None);
            (teamId, inviteCode) = (team.Id, team.InviteCode);
        }

        await using (var scope = Participation(DuringRegistration.AddMinutes(5)))
        {
            await scope.Service.JoinTeamAsync(inviteCode, member, CancellationToken.None);
        }

        await using var db = Fixture.CreateContext();
        var loaded = await db.Teams.Include(t => t.Members).SingleAsync(t => t.Id == teamId);
        Assert.Equal(("Null Pointers", "NULL POINTERS", leader, inviteCode), (loaded.Name, loaded.NormalizedName, loaded.LeaderUserId, loaded.InviteCode));
        Assert.Equal([leader, member], loaded.Members.OrderBy(m => m.JoinedAt).Select(m => m.UserId));
        Assert.All(loaded.Members, m => Assert.Equal((contest.Id, DateTimeKind.Utc), (m.ContestId, m.JoinedAt.Kind)));

        var registration = await db.ContestRegistrations.SingleAsync(r => r.ContestId == contest.Id);
        Assert.Equal((teamId, (Guid?)null), (registration.TeamId, registration.StudentUserId));
    }

    [Fact]
    public async Task Submission_lifecycle_and_final_standings_round_trip()
    {
        var contest = await SaveAsync(NewPublishedContest());
        var student = await AddStudentAsync();
        var organizer = await AddStudentAsync();
        ContestRegistration registration;
        await using (var scope = Participation(DuringRegistration))
        {
            registration = await scope.Service.RegisterStudentAsync(contest.Id, student, CancellationToken.None);
        }

        var accepted = Submission.Create(contest, contest.Problems.Single(), registration, null, student, "C++17", "int main() {}", DuringContest);
        accepted.StartProcessing();
        accepted.RecordRemoteSubmission("RUN-42");
        accepted.Complete(Verdict.Accepted, 15, 2048, DuringContest.AddSeconds(30));
        var cancelled = Submission.Create(contest, contest.Problems.Single(), registration, null, student, "Python 3", "print(1)", DuringContest.AddMinutes(1));
        cancelled.Cancel("Duplicate remote run", organizer, AfterContest);

        await using (var db = Fixture.CreateContext())
        {
            db.Submissions.AddRange(accepted, cancelled);
            await db.SaveChangesAsync();
        }

        await using (var db = Fixture.CreateContext())
        {
            var tracked = await db.Contests.Include(c => c.Problems).SingleAsync(c => c.Id == contest.Id);
            var submissions = await db.Submissions.ToListAsync();
            var standings = ContestFinalizer.Finalize(tracked, [new(registration.Id, "Test Student")], submissions, AfterContest);
            db.FinalStandings.AddRange(standings);
            await db.SaveChangesAsync();
        }

        await using var verify = Fixture.CreateContext();
        var loadedAccepted = await verify.Submissions.SingleAsync(s => s.Id == accepted.Id);
        Assert.Equal((SubmissionStatus.Completed, Verdict.Accepted, "RUN-42", 15, 2048), (loadedAccepted.Status, loadedAccepted.Verdict!.Value, loadedAccepted.RemoteSubmissionRef!, loadedAccepted.ExecutionTimeMs!.Value, loadedAccepted.MemoryKb!.Value));
        Assert.Equal(DateTimeKind.Utc, loadedAccepted.JudgedAt!.Value.Kind);

        var loadedCancelled = await verify.Submissions.SingleAsync(s => s.Id == cancelled.Id);
        Assert.Equal((SubmissionStatus.Cancelled, "Duplicate remote run", organizer), (loadedCancelled.Status, loadedCancelled.CancellationReason!, loadedCancelled.CancelledByUserId!.Value));

        var standing = await verify.FinalStandings.SingleAsync();
        Assert.Equal((registration.Id, 1, 1, 30, "Test Student"), (standing.RegistrationId, standing.Rank, standing.SolvedCount, standing.TotalPenalty, standing.DisplayName));

        var finalized = await verify.Contests.SingleAsync(c => c.Id == contest.Id);
        Assert.Equal((ContestStatus.Finalized, AfterContest), (finalized.Status, finalized.FinalizedAt!.Value));
    }
}
