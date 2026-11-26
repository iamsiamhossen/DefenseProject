using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed record ContestDraft(string Title, string? Description, ContestMode Mode, ContestSchedule Schedule);

public sealed class ContestManagementService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<Contest> CreateAsync(ContestDraft draft, CancellationToken cancellationToken)
    {
        var contest = Contest.Create(draft.Title, draft.Description, draft.Mode, draft.Schedule, UtcNow());
        db.Contests.Add(contest);
        await db.SaveChangesAsync(cancellationToken);
        return contest;
    }

    public async Task<Contest?> UpdateAsync(Guid contestId, ContestDraft draft, CancellationToken cancellationToken)
    {
        await using var transaction = await ContestRowLock.BeginAsync(db, contestId, cancellationToken);
        var contest = await db.Contests.SingleOrDefaultAsync(c => c.Id == contestId, cancellationToken);
        if (contest is null)
        {
            return null;
        }

        var now = UtcNow();
        var hasRegistrations = await db.ContestRegistrations.AnyAsync(r => r.ContestId == contestId, cancellationToken);
        contest.UpdateDetails(draft.Title, draft.Description, now);
        contest.ChangeMode(draft.Mode, hasRegistrations, now);
        contest.Reschedule(draft.Schedule, now);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return contest;
    }

    public async Task<Contest?> PublishAsync(Guid contestId, CancellationToken cancellationToken)
    {
        await using var transaction = await ContestRowLock.BeginAsync(db, contestId, cancellationToken);
        var contest = await db.Contests
            .Include(c => c.Problems)
            .ThenInclude(p => p.TestCases)
            .SingleOrDefaultAsync(c => c.Id == contestId, cancellationToken);
        if (contest is null)
        {
            return null;
        }

        contest.Publish(UtcNow());

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return contest;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
