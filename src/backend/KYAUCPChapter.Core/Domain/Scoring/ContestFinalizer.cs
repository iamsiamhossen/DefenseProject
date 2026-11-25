using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Submissions;

namespace KYAUCPChapter.Core.Domain.Scoring;

public static class ContestFinalizer
{
    public static IReadOnlyList<FinalStanding> Finalize(
        Contest contest,
        IReadOnlyCollection<ScoreboardEntry> entries,
        IReadOnlyCollection<Submission> submissions,
        DateTime now)
    {
        if (contest.GetPhase(now) != ContestPhase.Ended)
        {
            throw new DomainException("Only an ended contest can be finalized.");
        }

        if (submissions.Any(s => !s.IsResolved))
        {
            throw new DomainException("Every submission must be Completed or Cancelled before finalization.");
        }

        var standings = ScoreboardCalculator.CalculateFull(contest, entries, submissions)
            .Select(FinalStanding.From)
            .ToList();

        contest.MarkFinalized(now);
        return standings;
    }
}
