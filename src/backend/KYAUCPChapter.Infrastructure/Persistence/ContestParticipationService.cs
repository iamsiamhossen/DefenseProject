using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed class ContestParticipationService(AppDbContext db, TimeProvider timeProvider)
{
    private const int DuplicateEntryErrorNumber = 1062;

    public Task<ContestRegistration> RegisterStudentAsync(Guid contestId, Guid studentUserId, CancellationToken cancellationToken) =>
        InLockedContestAsync(contestId, async (contest, now) =>
        {
            await EnsureNotParticipatingAsync(contestId, studentUserId, cancellationToken);
            var registration = ContestRegistration.ForStudent(contest, studentUserId, now);
            db.ContestRegistrations.Add(registration);
            return registration;
        }, cancellationToken);

    public Task<Team> CreateTeamAsync(Guid contestId, string name, Guid leaderUserId, CancellationToken cancellationToken) =>
        InLockedContestAsync(contestId, async (contest, now) =>
        {
            await EnsureNotParticipatingAsync(contestId, leaderUserId, cancellationToken);
            var team = Team.Create(contest, name, leaderUserId, now);
            if (await db.Teams.AnyAsync(t => t.ContestId == contestId && t.NormalizedName == team.NormalizedName, cancellationToken))
            {
                throw new DomainException($"Team name '{team.Name}' is already used in this contest.");
            }

            db.Teams.Add(team);
            db.ContestRegistrations.Add(ContestRegistration.ForTeam(contest, team, now));
            return team;
        }, cancellationToken);

    public async Task<Team> JoinTeamAsync(string inviteCode, Guid userId, CancellationToken cancellationToken)
    {
        var code = Guard(inviteCode);
        var contestId = await db.Teams
            .Where(t => t.InviteCode == code)
            .Select(t => (Guid?)t.ContestId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("The invitation code is not valid.");

        return await InLockedContestAsync(contestId, async (contest, now) =>
        {
            var team = await db.Teams.Include(t => t.Members).SingleAsync(t => t.InviteCode == code, cancellationToken);
            await EnsureNotParticipatingAsync(contestId, userId, cancellationToken);
            team.Join(contest, userId, now);
            return team;
        }, cancellationToken);
    }

    private static string Guard(string inviteCode) =>
        string.IsNullOrWhiteSpace(inviteCode)
            ? throw new DomainException("The invitation code is not valid.")
            : inviteCode.Trim().ToUpperInvariant();

    private async Task<T> InLockedContestAsync<T>(Guid contestId, Func<Contest, DateTime, Task<T>> action, CancellationToken cancellationToken)
    {
        await using var transaction = await ContestRowLock.BeginAsync(db, contestId, cancellationToken);
        var contest = await db.Contests.SingleOrDefaultAsync(c => c.Id == contestId, cancellationToken)
            ?? throw new DomainException("Contest was not found.");

        var result = await action(contest, timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: DuplicateEntryErrorNumber })
        {
            throw new DomainException("The student already participates in this contest, or the team name is already used.");
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task EnsureNotParticipatingAsync(Guid contestId, Guid userId, CancellationToken cancellationToken)
    {
        var participates =
            await db.ContestRegistrations.AnyAsync(r => r.ContestId == contestId && r.StudentUserId == userId, cancellationToken)
            || await db.TeamMembers.AnyAsync(m => m.ContestId == contestId && m.UserId == userId, cancellationToken);

        if (participates)
        {
            throw new DomainException("The student already participates in this contest.");
        }
    }
}
