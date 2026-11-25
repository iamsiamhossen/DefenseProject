using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Teams;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Registrations;

public class ContestRegistrationTests
{
    [Fact]
    public void Student_registration_has_only_a_student()
    {
        var contest = PublishedContest();
        var student = Guid.NewGuid();

        var registration = ContestRegistration.ForStudent(contest, student, DuringRegistration);

        Assert.Equal(student, registration.StudentUserId);
        Assert.Null(registration.TeamId);
        Assert.Equal(contest.Id, registration.ContestId);
        Assert.Equal(DuringRegistration, registration.RegisteredAt);
    }

    [Fact]
    public void Team_registration_has_only_a_team()
    {
        var contest = PublishedContest(ContestMode.Team);
        var team = Team.Create(contest, "Team", Guid.NewGuid(), DuringRegistration);

        var registration = ContestRegistration.ForTeam(contest, team, DuringRegistration);

        Assert.Equal(team.Id, registration.TeamId);
        Assert.Null(registration.StudentUserId);
    }

    [Fact]
    public void Registration_type_must_match_contest_mode()
    {
        var teamContest = PublishedContest(ContestMode.Team);

        Assert.Throws<DomainException>(() => ContestRegistration.ForStudent(teamContest, Guid.NewGuid(), DuringRegistration));
    }

    [Fact]
    public void Team_must_belong_to_the_contest()
    {
        var contest = PublishedContest(ContestMode.Team);
        var other = PublishedContest(ContestMode.Team);
        var team = Team.Create(other, "Team", Guid.NewGuid(), DuringRegistration);

        Assert.Throws<ArgumentException>(() => ContestRegistration.ForTeam(contest, team, DuringRegistration));
    }

    [Fact]
    public void Late_registration_is_not_allowed()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => ContestRegistration.ForStudent(contest, Guid.NewGuid(), ClosesAt));
        Assert.Throws<DomainException>(() => ContestRegistration.ForStudent(contest, Guid.NewGuid(), DuringContest));
    }

    [Fact]
    public void Draft_contest_does_not_accept_registrations()
    {
        Assert.Throws<DomainException>(() => ContestRegistration.ForStudent(DraftContest(), Guid.NewGuid(), DuringRegistration));
    }

    [Fact]
    public void Student_can_withdraw_before_registration_closes()
    {
        var contest = PublishedContest();
        var registration = RegisterStudent(contest);

        registration.EnsureCanWithdraw(contest, ClosesAt.AddTicks(-1));
        Assert.Throws<DomainException>(() => registration.EnsureCanWithdraw(contest, ClosesAt));
    }

    [Fact]
    public void Team_registration_is_not_withdrawn_directly()
    {
        var contest = PublishedContest(ContestMode.Team);
        var team = Team.Create(contest, "Team", Guid.NewGuid(), DuringRegistration);
        var registration = ContestRegistration.ForTeam(contest, team, DuringRegistration);

        Assert.Throws<DomainException>(() => registration.EnsureCanWithdraw(contest, DuringRegistration));
    }
}
