using System.Security.Cryptography;
using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;

namespace KYAUCPChapter.Core.Domain.Teams;

public sealed class Team
{
    public const int MaxMembers = 3;
    public const int InviteCodeLength = 10;

    private const string InviteCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly List<TeamMember> _members = [];

    private Team(Guid id, Guid contestId, string name, string normalizedName, Guid leaderUserId, string inviteCode)
    {
        Id = id;
        ContestId = contestId;
        Name = name;
        NormalizedName = normalizedName;
        LeaderUserId = leaderUserId;
        InviteCode = inviteCode;
    }

    public Guid Id { get; private set; }

    public Guid ContestId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public Guid LeaderUserId { get; private set; }

    public string InviteCode { get; private set; }

    public IReadOnlyList<TeamMember> Members => _members.AsReadOnly();

    public static Team Create(Contest contest, string name, Guid leaderUserId, DateTime now)
    {
        EnsureTeamRegistrationOpen(contest, now);
        var teamName = Guard.Required(name, "Team name");
        var team = new Team(
            Guid.CreateVersion7(),
            contest.Id,
            teamName,
            teamName.ToUpperInvariant(),
            leaderUserId,
            RandomNumberGenerator.GetString(InviteCodeAlphabet, InviteCodeLength));
        team._members.Add(new TeamMember(team.Id, leaderUserId, contest.Id, now));
        return team;
    }

    public bool HasMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public void Join(Contest contest, Guid userId, DateTime now)
    {
        EnsureOpenFor(contest, now);
        if (HasMember(userId))
        {
            throw new DomainException("The student is already a member of this team.");
        }

        if (_members.Count >= MaxMembers)
        {
            throw new DomainException($"A team can have at most {MaxMembers} members.");
        }

        _members.Add(new TeamMember(Id, userId, ContestId, now));
    }

    public void Leave(Contest contest, Guid userId, DateTime now)
    {
        EnsureOpenFor(contest, now);
        var member = GetMember(userId);
        if (userId == LeaderUserId)
        {
            throw new DomainException(_members.Count == 1
                ? "A sole-member leader must disband the team instead of leaving."
                : "The leader must transfer leadership before leaving the team.");
        }

        _members.Remove(member);
    }

    public void RemoveMember(Contest contest, Guid actingUserId, Guid memberUserId, DateTime now)
    {
        EnsureOpenFor(contest, now);
        EnsureLeader(actingUserId);
        if (memberUserId == LeaderUserId)
        {
            throw new DomainException("The leader cannot be removed from the team.");
        }

        _members.Remove(GetMember(memberUserId));
    }

    public void TransferLeadership(Contest contest, Guid actingUserId, Guid newLeaderUserId, DateTime now)
    {
        EnsureOpenFor(contest, now);
        EnsureLeader(actingUserId);
        if (newLeaderUserId == LeaderUserId)
        {
            throw new DomainException("The student is already the team leader.");
        }

        LeaderUserId = GetMember(newLeaderUserId).UserId;
    }

    public void EnsureCanBeDisbandedBy(Contest contest, Guid actingUserId, DateTime now)
    {
        EnsureOpenFor(contest, now);
        EnsureLeader(actingUserId);
    }

    private static void EnsureTeamRegistrationOpen(Contest contest, DateTime now)
    {
        if (contest.Mode != ContestMode.Team)
        {
            throw new DomainException("Teams can only be used in Team mode contests.");
        }

        if (!contest.IsRegistrationOpen(now))
        {
            throw new DomainException("Team changes are only allowed while registration is open.");
        }
    }

    private void EnsureOpenFor(Contest contest, DateTime now)
    {
        if (contest.Id != ContestId)
        {
            throw new ArgumentException("The team does not belong to this contest.", nameof(contest));
        }

        EnsureTeamRegistrationOpen(contest, now);
    }

    private void EnsureLeader(Guid userId)
    {
        if (userId != LeaderUserId)
        {
            throw new DomainException("Only the team leader can perform this action.");
        }
    }

    private TeamMember GetMember(Guid userId) =>
        _members.SingleOrDefault(m => m.UserId == userId)
        ?? throw new DomainException("The student is not a member of this team.");
}
