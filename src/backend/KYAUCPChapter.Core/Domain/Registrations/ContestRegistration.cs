using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Teams;

namespace KYAUCPChapter.Core.Domain.Registrations;

public sealed class ContestRegistration
{
    private ContestRegistration(Guid contestId, Guid? studentUserId, Guid? teamId, DateTime registeredAt)
    {
        Id = Guid.CreateVersion7();
        ContestId = contestId;
        StudentUserId = studentUserId;
        TeamId = teamId;
        RegisteredAt = registeredAt;
    }

    public Guid Id { get; private set; }

    public Guid ContestId { get; private set; }

    public Guid? StudentUserId { get; private set; }

    public Guid? TeamId { get; private set; }

    public DateTime RegisteredAt { get; private set; }

    public static ContestRegistration ForStudent(Contest contest, Guid studentUserId, DateTime now)
    {
        EnsureRegistrationOpen(contest, ContestMode.Individual, now);
        return new ContestRegistration(contest.Id, studentUserId, null, now);
    }

    public static ContestRegistration ForTeam(Contest contest, Team team, DateTime now)
    {
        if (team.ContestId != contest.Id)
        {
            throw new ArgumentException("The team does not belong to this contest.", nameof(team));
        }

        EnsureRegistrationOpen(contest, ContestMode.Team, now);
        return new ContestRegistration(contest.Id, null, team.Id, now);
    }

    public void EnsureCanWithdraw(Contest contest, DateTime now)
    {
        if (contest.Id != ContestId)
        {
            throw new ArgumentException("The registration does not belong to this contest.", nameof(contest));
        }

        if (TeamId is not null)
        {
            throw new DomainException("A team registration is withdrawn by disbanding the team.");
        }

        if (!contest.IsRegistrationOpen(now))
        {
            throw new DomainException("Withdrawal is only allowed while registration is open.");
        }
    }

    private static void EnsureRegistrationOpen(Contest contest, ContestMode mode, DateTime now)
    {
        if (contest.Mode != mode)
        {
            throw new DomainException($"This contest does not accept {mode} registrations.");
        }

        if (!contest.IsRegistrationOpen(now))
        {
            throw new DomainException("Registration is not open for this contest.");
        }
    }
}
