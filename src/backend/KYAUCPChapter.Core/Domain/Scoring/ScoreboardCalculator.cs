using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Submissions;

namespace KYAUCPChapter.Core.Domain.Scoring;

public static class ScoreboardCalculator
{
    public const int WrongAttemptPenaltyMinutes = 20;

    public static IReadOnlyList<ScoreboardRow> CalculatePublic(
        Contest contest,
        IEnumerable<ScoreboardEntry> entries,
        IEnumerable<Submission> submissions) =>
        Calculate(contest, entries, submissions, contest.Schedule.FreezeAt);

    public static IReadOnlyList<ScoreboardRow> CalculateFull(
        Contest contest,
        IEnumerable<ScoreboardEntry> entries,
        IEnumerable<Submission> submissions) =>
        Calculate(contest, entries, submissions, null);

    private static IReadOnlyList<ScoreboardRow> Calculate(
        Contest contest,
        IEnumerable<ScoreboardEntry> entries,
        IEnumerable<Submission> submissions,
        DateTime? cutoff)
    {
        var countedByRegistration = submissions
            .Where(s => s.Status == SubmissionStatus.Completed && (cutoff is null || s.ReceivedAt < cutoff))
            .ToLookup(s => s.RegistrationId);

        var problems = contest.Problems.OrderBy(p => p.Label, StringComparer.OrdinalIgnoreCase).ToList();

        var ordered = entries
            .Select(entry => BuildRow(contest, problems, entry, countedByRegistration[entry.RegistrationId]))
            .OrderByDescending(row => row.SolvedCount)
            .ThenBy(row => row.TotalPenalty)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RegistrationId)
            .ToList();

        var ranked = new List<ScoreboardRow>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var row = ordered[i];
            var tiesPrevious = i > 0
                && row.SolvedCount == ordered[i - 1].SolvedCount
                && row.TotalPenalty == ordered[i - 1].TotalPenalty;
            ranked.Add(row with { Rank = tiesPrevious ? ranked[i - 1].Rank : i + 1 });
        }

        return ranked;
    }

    private static ScoreboardRow BuildRow(
        Contest contest,
        IReadOnlyList<Problem> problems,
        ScoreboardEntry entry,
        IEnumerable<Submission> submissions)
    {
        var byProblem = submissions.ToLookup(s => s.ProblemId);
        var results = problems
            .Select(p => ScoreProblem(contest.Schedule.StartsAt, p.Id, byProblem[p.Id]))
            .ToList();

        var solved = results.Where(r => r.IsSolved).ToList();
        var totalPenalty = solved.Sum(r => r.SolvedAtMinute!.Value + WrongAttemptPenaltyMinutes * r.PenalizedAttempts);

        return new ScoreboardRow(0, entry.RegistrationId, entry.DisplayName, solved.Count, totalPenalty, results);
    }

    private static ProblemResult ScoreProblem(DateTime contestStartsAt, Guid problemId, IEnumerable<Submission> submissions)
    {
        var penalizedAttempts = 0;
        foreach (var submission in submissions.OrderBy(s => s.ReceivedAt).ThenBy(s => s.Id))
        {
            if (submission.Verdict == Verdict.Accepted)
            {
                var minute = (int)((submission.ReceivedAt - contestStartsAt).Ticks / TimeSpan.TicksPerMinute);
                return new ProblemResult(problemId, true, penalizedAttempts, minute);
            }

            if (IsPenalized(submission.Verdict))
            {
                penalizedAttempts++;
            }
        }

        return new ProblemResult(problemId, false, penalizedAttempts, null);
    }

    private static bool IsPenalized(Verdict? verdict) => verdict is
        Verdict.WrongAnswer
        or Verdict.TimeLimitExceeded
        or Verdict.MemoryLimitExceeded
        or Verdict.RuntimeError;
}
