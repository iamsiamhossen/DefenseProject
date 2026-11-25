namespace KYAUCPChapter.Core.Domain.Teams;

public sealed class TeamMember
{
    internal TeamMember(Guid teamId, Guid userId, Guid contestId, DateTime joinedAt)
    {
        TeamId = teamId;
        UserId = userId;
        ContestId = contestId;
        JoinedAt = joinedAt;
    }

    public Guid TeamId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ContestId { get; private set; }

    public DateTime JoinedAt { get; private set; }
}
