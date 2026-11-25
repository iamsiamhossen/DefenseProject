using System.Data.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Teams;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Tests;

public sealed class DatabaseConstraintTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Team_names_are_unique_per_contest_ignoring_case()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var otherContest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var first = Team.Create(contest, "Null Pointers", await AddStudentAsync(), DuringRegistration);
        var sameNameOtherContest = Team.Create(otherContest, "Null Pointers", await AddStudentAsync(), DuringRegistration);
        var duplicate = Team.Create(contest, "null pointers", await AddStudentAsync(), DuringRegistration);

        await using (var db = Fixture.CreateContext())
        {
            db.Teams.AddRange(first, sameNameOtherContest);
            await db.SaveChangesAsync();
        }

        await using var conflicting = Fixture.CreateContext();
        conflicting.Teams.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_problem_labels_differing_only_in_case_are_rejected()
    {
        var contest = await SaveAsync(NewContest());
        await using var first = Fixture.CreateContext();
        await using var second = Fixture.CreateContext();
        var firstCopy = await first.Contests.Include(c => c.Problems).SingleAsync(c => c.Id == contest.Id);
        var secondCopy = await second.Contests.Include(c => c.Problems).SingleAsync(c => c.Id == contest.Id);

        firstCopy.AddTophProblem("C", "First", "first", "https://toph.co/p/first", Now);
        secondCopy.AddTophProblem("c", "Second", "second", "https://toph.co/p/second", Now);
        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Problem_labels_differing_by_accent_are_distinct()
    {
        var contest = NewContest();
        contest.AddTophProblem("E", "Plain", "plain", "https://toph.co/p/plain", Now);
        contest.AddTophProblem("É", "Accented", "accented", "https://toph.co/p/accented", Now);

        await SaveAsync(contest);

        await using var db = Fixture.CreateContext();
        Assert.Equal(2, await db.Set<Problem>().CountAsync(p => p.ContestId == contest.Id));
    }

    [Fact]
    public async Task Student_cannot_have_two_individual_registrations_in_a_contest()
    {
        var contest = await SaveAsync(NewPublishedContest());
        var student = await AddStudentAsync();

        await using (var db = Fixture.CreateContext())
        {
            db.ContestRegistrations.Add(ContestRegistration.ForStudent(contest, student, DuringRegistration));
            await db.SaveChangesAsync();
        }

        await using var conflicting = Fixture.CreateContext();
        conflicting.ContestRegistrations.Add(ContestRegistration.ForStudent(contest, student, DuringRegistration));
        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }

    [Fact]
    public async Task Student_cannot_be_a_member_of_two_teams_in_a_contest()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var student = await AddStudentAsync();
        var firstTeam = Team.Create(contest, "First", student, DuringRegistration);
        var secondTeam = Team.Create(contest, "Second", await AddStudentAsync(), DuringRegistration);
        secondTeam.Join(contest, student, DuringRegistration);

        await using (var db = Fixture.CreateContext())
        {
            db.Teams.Add(firstTeam);
            await db.SaveChangesAsync();
        }

        await using var conflicting = Fixture.CreateContext();
        conflicting.Teams.Add(secondTeam);
        await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());
    }

    [Fact]
    public async Task Registration_must_reference_exactly_one_participant()
    {
        var contest = await SaveAsync(NewPublishedContest());

        await using var db = Fixture.CreateContext();
        var error = await Assert.ThrowsAnyAsync<DbException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO `ContestRegistrations` (`Id`, `ContestId`, `StudentUserId`, `TeamId`, `RegisteredAt`) VALUES ({Guid.NewGuid().ToString()}, {contest.Id.ToString()}, NULL, NULL, {DuringRegistration})"));

        Assert.Contains("CK_ContestRegistrations_SingleParticipant", error.Message);
    }

    [Fact]
    public async Task Finalized_status_requires_a_finalization_time()
    {
        var contest = await SaveAsync(NewPublishedContest());

        await using var db = Fixture.CreateContext();
        var error = await Assert.ThrowsAnyAsync<DbException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE `Contests` SET `Status` = 'Finalized' WHERE `Id` = {contest.Id.ToString()}"));

        Assert.Contains("CK_Contests_FinalizedAt", error.Message);
    }

    [Fact]
    public async Task Freeze_time_outside_the_contest_is_rejected()
    {
        var contest = await SaveAsync(NewPublishedContest());

        await using var db = Fixture.CreateContext();
        var error = await Assert.ThrowsAnyAsync<DbException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE `Contests` SET `FreezeAt` = `StartsAt` WHERE `Id` = {contest.Id.ToString()}"));

        Assert.Contains("CK_Contests_FreezeAt", error.Message);
    }

    [Fact]
    public async Task Normalized_student_ids_are_unique()
    {
        await AddStudentAsync("221-15-001");

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddStudentAsync("221-15-001"));

        Assert.True(HasMessage(error, nameof(ApplicationUser.NormalizedStudentId)));
    }
}
