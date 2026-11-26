using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed record InternalProblemInput(string Label, string Title, string Statement, int TimeLimitMs, int MemoryLimitKb);

public sealed record TophProblemInput(string Label, string Title, string ExternalProblemId, string ExternalProblemUrl);

public sealed class ProblemManagementService(AppDbContext db, TimeProvider timeProvider)
{
    public Task<Problem?> AddInternalAsync(Guid contestId, InternalProblemInput input, IReadOnlyCollection<TestCaseContent> testCases, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, null, (contest, now) =>
            contest.AddInternalProblem(input.Label, input.Title, input.Statement, input.TimeLimitMs, input.MemoryLimitKb, testCases, now), cancellationToken);

    public Task<Problem?> AddTophAsync(Guid contestId, TophProblemInput input, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, null, (contest, now) =>
            contest.AddTophProblem(input.Label, input.Title, input.ExternalProblemId, input.ExternalProblemUrl, now), cancellationToken);

    public Task<Problem?> UpdateInternalAsync(Guid contestId, Guid problemId, InternalProblemInput input, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, problemId, (contest, now) =>
        {
            contest.UpdateInternalProblem(problemId, input.Label, input.Title, input.Statement, input.TimeLimitMs, input.MemoryLimitKb, now);
            return Find(contest, problemId);
        }, cancellationToken);

    public Task<Problem?> UpdateTophAsync(Guid contestId, Guid problemId, TophProblemInput input, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, problemId, (contest, now) =>
        {
            contest.UpdateTophProblem(problemId, input.Label, input.Title, input.ExternalProblemId, input.ExternalProblemUrl, now);
            return Find(contest, problemId);
        }, cancellationToken);

    public Task<Problem?> RemoveAsync(Guid contestId, Guid problemId, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, problemId, (contest, now) =>
        {
            var problem = Find(contest, problemId);
            contest.RemoveProblem(problemId, now);
            return problem;
        }, cancellationToken);

    public Task<TestCase?> AddTestCaseAsync(Guid contestId, Guid problemId, TestCaseContent content, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, problemId, (contest, now) =>
            contest.AddTestCase(problemId, content.Input, content.ExpectedOutput, content.IsSample, now), cancellationToken);

    public Task<TestCase?> UpdateTestCaseAsync(Guid contestId, Guid problemId, int position, string input, string expectedOutput, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, problemId, (contest, now) =>
        {
            var testCase = FindTestCase(contest, problemId, position);
            if (testCase is not null)
            {
                contest.UpdateTestCase(problemId, position, input, expectedOutput, now);
            }

            return testCase;
        }, cancellationToken);

    public Task<TestCase?> RemoveTestCaseAsync(Guid contestId, Guid problemId, int position, CancellationToken cancellationToken) =>
        ModifyAsync(contestId, problemId, (contest, now) =>
        {
            var testCase = FindTestCase(contest, problemId, position);
            if (testCase is not null)
            {
                contest.RemoveTestCase(problemId, position, now);
            }

            return testCase;
        }, cancellationToken);

    private async Task<T?> ModifyAsync<T>(Guid contestId, Guid? problemId, Func<Contest, DateTime, T?> change, CancellationToken cancellationToken)
        where T : class
    {
        await using var transaction = await ContestRowLock.BeginAsync(db, contestId, cancellationToken);
        IQueryable<Contest> query = problemId is { } id
            ? db.Contests.Include(c => c.Problems).ThenInclude(p => p.TestCases.Where(t => t.ProblemId == id))
            : db.Contests.Include(c => c.Problems);
        var contest = await query.SingleOrDefaultAsync(c => c.Id == contestId, cancellationToken);
        if (contest is null || (problemId is { } requested && !contest.HasProblem(requested)))
        {
            return null;
        }

        var result = change(contest, timeProvider.GetUtcNow().UtcDateTime);
        if (result is null)
        {
            return null;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static Problem Find(Contest contest, Guid problemId) => contest.Problems.Single(p => p.Id == problemId);

    private static TestCase? FindTestCase(Contest contest, Guid problemId, int position) =>
        Find(contest, problemId).TestCases.SingleOrDefault(t => t.Position == position);
}
