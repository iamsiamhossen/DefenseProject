using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Tests;

public sealed class ContestParticipationServiceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Student_registers_individually_only_once()
    {
        var contest = await SaveAsync(NewPublishedContest());
        var student = await AddStudentAsync();

        await using (var scope = Participation(DuringRegistration))
        {
            await scope.Service.RegisterStudentAsync(contest.Id, student, CancellationToken.None);
        }

        await using var again = Participation(DuringRegistration);
        await Assert.ThrowsAsync<DomainException>(() => again.Service.RegisterStudentAsync(contest.Id, student, CancellationToken.None));
    }

    [Fact]
    public async Task Registration_uses_the_current_time_from_the_time_provider()
    {
        var contest = await SaveAsync(NewPublishedContest());

        await using var scope = Participation(ClosesAt);
        await Assert.ThrowsAsync<DomainException>(() => scope.Service.RegisterStudentAsync(contest.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_contest_is_rejected()
    {
        await using var scope = Participation(DuringRegistration);

        await Assert.ThrowsAsync<DomainException>(() => scope.Service.RegisterStudentAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Team_creation_writes_team_and_single_registration_atomically()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var leader = await AddStudentAsync();

        await using (var scope = Participation(DuringRegistration))
        {
            await scope.Service.CreateTeamAsync(contest.Id, "Null Pointers", leader, CancellationToken.None);
        }

        await using var db = Fixture.CreateContext();
        var team = await db.Teams.SingleAsync();
        Assert.Equal(team.Id, (await db.ContestRegistrations.SingleAsync()).TeamId);
    }

    [Fact]
    public async Task Team_member_cannot_create_or_join_another_team_in_the_contest()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var leader = await AddStudentAsync();
        var otherLeader = await AddStudentAsync();
        string otherInviteCode;
        await using (var scope = Participation(DuringRegistration))
        {
            await scope.Service.CreateTeamAsync(contest.Id, "First", leader, CancellationToken.None);
        }

        await using (var scope = Participation(DuringRegistration))
        {
            otherInviteCode = (await scope.Service.CreateTeamAsync(contest.Id, "Second", otherLeader, CancellationToken.None)).InviteCode;
        }

        await using (var scope = Participation(DuringRegistration))
        {
            await Assert.ThrowsAsync<DomainException>(() => scope.Service.CreateTeamAsync(contest.Id, "Third", leader, CancellationToken.None));
        }

        await using var joinScope = Participation(DuringRegistration);
        await Assert.ThrowsAsync<DomainException>(() => joinScope.Service.JoinTeamAsync(otherInviteCode, leader, CancellationToken.None));

        await using var db = Fixture.CreateContext();
        Assert.Equal(2, await db.Teams.CountAsync());
        Assert.Equal(2, await db.TeamMembers.CountAsync());
    }

    [Fact]
    public async Task Team_names_are_rejected_when_already_used_ignoring_case()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        await using (var scope = Participation(DuringRegistration))
        {
            await scope.Service.CreateTeamAsync(contest.Id, "Null Pointers", await AddStudentAsync(), CancellationToken.None);
        }

        await using var duplicate = Participation(DuringRegistration);
        var error = await Assert.ThrowsAsync<DomainException>(() => duplicate.Service.CreateTeamAsync(contest.Id, "NULL POINTERS", Guid.NewGuid(), CancellationToken.None));

        Assert.Contains("already used", error.Message);
    }

    [Fact]
    public async Task Invite_codes_are_matched_ignoring_case_and_whitespace()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        var member = await AddStudentAsync();
        string inviteCode;
        await using (var scope = Participation(DuringRegistration))
        {
            inviteCode = (await scope.Service.CreateTeamAsync(contest.Id, "Null Pointers", await AddStudentAsync(), CancellationToken.None)).InviteCode;
        }

        await using (var scope = Participation(DuringRegistration))
        {
            var team = await scope.Service.JoinTeamAsync($"  {inviteCode.ToLowerInvariant()} ", member, CancellationToken.None);
            Assert.True(team.HasMember(member));
        }

        var outsider = await AddStudentAsync();
        await using var invalid = Participation(DuringRegistration);
        await Assert.ThrowsAsync<DomainException>(() => invalid.Service.JoinTeamAsync("NOTACODE00", outsider, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_joins_never_exceed_team_capacity()
    {
        var contest = await SaveAsync(NewPublishedContest(ContestMode.Team));
        string inviteCode;
        await using (var scope = Participation(DuringRegistration))
        {
            inviteCode = (await scope.Service.CreateTeamAsync(contest.Id, "Null Pointers", await AddStudentAsync(), CancellationToken.None)).InviteCode;
        }

        await using (var scope = Participation(DuringRegistration))
        {
            await scope.Service.JoinTeamAsync(inviteCode, await AddStudentAsync(), CancellationToken.None);
        }

        var joiners = new[] { await AddStudentAsync(), await AddStudentAsync(), await AddStudentAsync() };
        var results = await Task.WhenAll(joiners.Select(user => TryAsync(async () =>
        {
            await using var scope = Participation(DuringRegistration);
            await scope.Service.JoinTeamAsync(inviteCode, user, CancellationToken.None);
        })));

        Assert.Equal(1, results.Count(succeeded => succeeded));
        await using var db = Fixture.CreateContext();
        Assert.Equal(3, await db.TeamMembers.CountAsync());
    }

    [Fact]
    public async Task Concurrent_registrations_for_the_same_student_create_one_entry()
    {
        var contest = await SaveAsync(NewPublishedContest());
        var student = await AddStudentAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => TryAsync(async () =>
        {
            await using var scope = Participation(DuringRegistration);
            await scope.Service.RegisterStudentAsync(contest.Id, student, CancellationToken.None);
        })));

        Assert.Equal(1, results.Count(succeeded => succeeded));
        await using var db = Fixture.CreateContext();
        Assert.Equal(1, await db.ContestRegistrations.CountAsync());
    }

    private static async Task<bool> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
