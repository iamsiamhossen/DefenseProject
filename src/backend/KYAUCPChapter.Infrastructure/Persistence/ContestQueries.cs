using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed class ContestQueries(AppDbContext db)
{
    public Task<List<Contest>> ListAsync(bool includeDrafts, CancellationToken cancellationToken) =>
        Visible(includeDrafts)
            .OrderByDescending(c => c.Schedule.StartsAt)
            .ToListAsync(cancellationToken);

    public Task<Contest?> FindAsync(Guid contestId, bool includeDrafts, CancellationToken cancellationToken) =>
        Visible(includeDrafts).SingleOrDefaultAsync(c => c.Id == contestId, cancellationToken);

    public Task<int> CountProblemsAsync(Guid contestId, CancellationToken cancellationToken) =>
        db.Set<Problem>().CountAsync(p => p.ContestId == contestId, cancellationToken);

    private IQueryable<Contest> Visible(bool includeDrafts) =>
        db.Contests.AsNoTracking().Where(c => includeDrafts || c.Status != ContestStatus.Draft);
}
