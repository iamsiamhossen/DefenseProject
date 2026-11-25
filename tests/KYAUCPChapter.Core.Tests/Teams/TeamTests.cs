using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Teams;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Teams;

public class TeamTests
{
    private readonly Contest _contest = PublishedContest(ContestMode.Team);
    private readonly Guid _leader = Guid.NewGuid();

    [Fact]
    public void Creator_becomes_leader_and_first_member()
    {
        var team = Team.Create(_contest, "  Null Pointers ", _leader, DuringRegistration);

        Assert.Equal("Null Pointers", team.Name);
        Assert.Equal("NULL POINTERS", team.NormalizedName);
        Assert.Equal(_leader, team.LeaderUserId);
        var member = Assert.Single(team.Members);
        Assert.Equal(_leader, member.UserId);
        Assert.Equal(_contest.Id, member.ContestId);
        Assert.Equal(Team.InviteCodeLength, team.InviteCode.Length);
    }

    [Fact]
    public void Invite_codes_are_random()
    {
        var first = Team.Create(_contest, "One", Guid.NewGuid(), DuringRegistration);
        var second = Team.Create(_contest, "Two", Guid.NewGuid(), DuringRegistration);

        Assert.NotEqual(first.InviteCode, second.InviteCode);
    }

    [Fact]
    public void Team_name_is_required()
    {
        Assert.Throws<DomainException>(() => Team.Create(_contest, " ", _leader, DuringRegistration));
    }

    [Fact]
    public void Teams_require_a_team_mode_contest()
    {
        var individual = PublishedContest(ContestMode.Individual);

        Assert.Throws<DomainException>(() => Team.Create(individual, "Team", _leader, DuringRegistration));
    }

    [Theory]
    [MemberData(nameof(OutsideRegistration))]
    public void Teams_cannot_be_created_outside_registration(DateTime now)
    {
        Assert.Throws<DomainException>(() => Team.Create(_contest, "Team", _leader, now));
    }

    public static TheoryData<DateTime> OutsideRegistration => new() { OpensAt.AddTicks(-1), ClosesAt };

    [Fact]
    public void Team_allows_up_to_three_members()
    {
        var team = CreateTeam();
        team.Join(_contest, Guid.NewGuid(), DuringRegistration);
        team.Join(_contest, Guid.NewGuid(), DuringRegistration);

        Assert.Equal(Team.MaxMembers, team.Members.Count);
        Assert.Throws<DomainException>(() => team.Join(_contest, Guid.NewGuid(), DuringRegistration));
    }

    [Fact]
    public void Member_cannot_join_twice()
    {
        var team = CreateTeam();

        Assert.Throws<DomainException>(() => team.Join(_contest, _leader, DuringRegistration));
    }

    [Fact]
    public void Membership_locks_at_registration_close()
    {
        var team = CreateTeam();
        var member = Guid.NewGuid();
        team.Join(_contest, member, DuringRegistration);

        Assert.Throws<DomainException>(() => team.Join(_contest, Guid.NewGuid(), ClosesAt));
        Assert.Throws<DomainException>(() => team.Leave(_contest, member, ClosesAt));
        Assert.Throws<DomainException>(() => team.RemoveMember(_contest, _leader, member, ClosesAt));
        Assert.Throws<DomainException>(() => team.TransferLeadership(_contest, _leader, member, ClosesAt));
        Assert.Throws<DomainException>(() => team.EnsureCanBeDisbandedBy(_contest, _leader, ClosesAt));
    }

    [Fact]
    public void Member_can_leave()
    {
        var team = CreateTeam();
        var member = Guid.NewGuid();
        team.Join(_contest, member, DuringRegistration);

        team.Leave(_contest, member, DuringRegistration);

        Assert.False(team.HasMember(member));
    }

    [Fact]
    public void Leader_must_transfer_leadership_before_leaving()
    {
        var team = CreateTeam();
        team.Join(_contest, Guid.NewGuid(), DuringRegistration);

        var error = Assert.Throws<DomainException>(() => team.Leave(_contest, _leader, DuringRegistration));
        Assert.Contains("transfer leadership", error.Message);
    }

    [Fact]
    public void Sole_leader_must_disband_instead_of_leaving()
    {
        var team = CreateTeam();

        var error = Assert.Throws<DomainException>(() => team.Leave(_contest, _leader, DuringRegistration));
        Assert.Contains("disband", error.Message);
        team.EnsureCanBeDisbandedBy(_contest, _leader, DuringRegistration);
    }

    [Fact]
    public void Non_member_cannot_leave()
    {
        var team = CreateTeam();

        Assert.Throws<DomainException>(() => team.Leave(_contest, Guid.NewGuid(), DuringRegistration));
    }

    [Fact]
    public void Leader_can_remove_a_member()
    {
        var team = CreateTeam();
        var member = Guid.NewGuid();
        team.Join(_contest, member, DuringRegistration);

        team.RemoveMember(_contest, _leader, member, DuringRegistration);

        Assert.False(team.HasMember(member));
    }

    [Fact]
    public void Only_leader_can_remove_members()
    {
        var team = CreateTeam();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        team.Join(_contest, first, DuringRegistration);
        team.Join(_contest, second, DuringRegistration);

        Assert.Throws<DomainException>(() => team.RemoveMember(_contest, first, second, DuringRegistration));
    }

    [Fact]
    public void Leader_cannot_remove_themself()
    {
        var team = CreateTeam();

        Assert.Throws<DomainException>(() => team.RemoveMember(_contest, _leader, _leader, DuringRegistration));
    }

    [Fact]
    public void Leadership_transfers_to_a_member_who_can_then_lead()
    {
        var team = CreateTeam();
        var member = Guid.NewGuid();
        team.Join(_contest, member, DuringRegistration);

        team.TransferLeadership(_contest, _leader, member, DuringRegistration);
        team.Leave(_contest, _leader, DuringRegistration);

        Assert.Equal(member, team.LeaderUserId);
        Assert.Single(team.Members);
    }

    [Fact]
    public void Leadership_cannot_transfer_to_a_non_member()
    {
        var team = CreateTeam();

        Assert.Throws<DomainException>(() => team.TransferLeadership(_contest, _leader, Guid.NewGuid(), DuringRegistration));
    }

    [Fact]
    public void Only_leader_can_disband()
    {
        var team = CreateTeam();
        var member = Guid.NewGuid();
        team.Join(_contest, member, DuringRegistration);

        Assert.Throws<DomainException>(() => team.EnsureCanBeDisbandedBy(_contest, member, DuringRegistration));
    }

    [Fact]
    public void Team_operations_require_its_own_contest()
    {
        var team = CreateTeam();
        var otherContest = PublishedContest(ContestMode.Team);

        Assert.Throws<ArgumentException>(() => team.Join(otherContest, Guid.NewGuid(), DuringRegistration));
    }

    private Team CreateTeam() => Team.Create(_contest, "Null Pointers", _leader, DuringRegistration);
}
