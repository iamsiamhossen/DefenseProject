using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed record ProblemSummary(
    Guid Id,
    string Label,
    string Title,
    ProblemType Type,
    int? TimeLimitMs,
    int? MemoryLimitKb,
    int SampleCount,
    int HiddenCount);

public sealed record TestCasePreview(int Position, bool IsSample, string InputPreview, int InputLength, string OutputPreview, int OutputLength);

public sealed record ProblemDetail(
    Guid Id,
    Guid ContestId,
    string Label,
    string Title,
    ProblemType Type,
    string? Statement,
    int? TimeLimitMs,
    int? MemoryLimitKb,
    string? ExternalProblemId,
    string? ExternalProblemUrl,
    IReadOnlyList<TestCasePreview> TestCases);

public sealed class ProblemQueries(AppDbContext db)
{
    public const int PreviewLength = 300;

    public Task<List<ProblemSummary>> ListAsync(Guid contestId, CancellationToken cancellationToken) =>
        db.Set<Problem>()
            .AsNoTracking()
            .Where(p => p.ContestId == contestId)
            .OrderBy(p => p.Label)
            .Select(p => new ProblemSummary(
                p.Id,
                p.Label,
                p.Title,
                p.Type,
                p.TimeLimitMs,
                p.MemoryLimitKb,
                p.TestCases.Count(t => t.IsSample),
                p.TestCases.Count(t => !t.IsSample)))
            .ToListAsync(cancellationToken);

    public async Task<ProblemDetail?> FindAsync(Guid contestId, Guid problemId, CancellationToken cancellationToken)
    {
        var problem = await db.Set<Problem>()
            .AsNoTracking()
            .Where(p => p.Id == problemId && p.ContestId == contestId)
            .Select(p => new { p.Id, p.ContestId, p.Label, p.Title, p.Type, p.Statement, p.TimeLimitMs, p.MemoryLimitKb, p.ExternalProblemId, p.ExternalProblemUrl })
            .SingleOrDefaultAsync(cancellationToken);
        if (problem is null)
        {
            return null;
        }

        var testCases = await db.Set<TestCase>()
            .AsNoTracking()
            .Where(t => t.ProblemId == problemId)
            .OrderBy(t => t.Position)
            .Select(t => new TestCasePreview(
                t.Position,
                t.IsSample,
                t.Input.Substring(0, PreviewLength),
                t.Input.Length,
                t.ExpectedOutput.Substring(0, PreviewLength),
                t.ExpectedOutput.Length))
            .ToListAsync(cancellationToken);

        return new ProblemDetail(
            problem.Id,
            problem.ContestId,
            problem.Label,
            problem.Title,
            problem.Type,
            problem.Statement,
            problem.TimeLimitMs,
            problem.MemoryLimitKb,
            problem.ExternalProblemId,
            problem.ExternalProblemUrl,
            testCases);
    }

    public Task<TestCase?> FindTestCaseAsync(Guid contestId, Guid problemId, int position, CancellationToken cancellationToken) =>
        db.Set<TestCase>()
            .AsNoTracking()
            .Where(t => t.ProblemId == problemId && t.Position == position)
            .Where(t => db.Set<Problem>().Any(p => p.Id == problemId && p.ContestId == contestId))
            .SingleOrDefaultAsync(cancellationToken);
}
