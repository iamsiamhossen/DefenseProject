using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Scoring;
using KYAUCPChapter.Core.Domain.Submissions;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Scoring;

public class ScoreboardCalculatorTests
{
    private readonly Contest _contest;
    private readonly Problem _a;
    private readonly Problem _b;

    public ScoreboardCalculatorTests()
    {
        _contest = DraftContest(freezeAt: FreezeAt);
        _a = AddJudgeableProblem(_contest, "A");
        _b = AddJudgeableProblem(_contest, "B");
        _contest.Publish(Now);
    }

    [Fact]
    public void Penalty_uses_floored_minutes_and_counted_wrong_attempts()
    {
        var registration = RegisterStudent(_contest);
        var submissions = new[]
        {
            Judged(_contest, _a, registration, At(10, 59), Verdict.WrongAnswer),
            Judged(_contest, _a, registration, At(11), Verdict.TimeLimitExceeded),
            Judged(_contest, _a, registration, At(12), Verdict.MemoryLimitExceeded),
            Judged(_contest, _a, registration, At(13), Verdict.RuntimeError),
            Judged(_contest, _a, registration, At(14), Verdict.CompilationError),
            Judged(_contest, _a, registration, At(25, 59), Verdict.Accepted)
        };

        var row = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], submissions));

        Assert.Equal(1, row.SolvedCount);
        Assert.Equal(25 + 4 * ScoreboardCalculator.WrongAttemptPenaltyMinutes, row.TotalPenalty);
        var result = row.Problems.Single(p => p.ProblemId == _a.Id);
        Assert.Equal(4, result.PenalizedAttempts);
        Assert.Equal(25, result.SolvedAtMinute);
    }

    [Fact]
    public void Submissions_after_first_accepted_do_not_affect_score()
    {
        var registration = RegisterStudent(_contest);
        var submissions = new[]
        {
            Judged(_contest, _a, registration, At(10), Verdict.Accepted),
            Judged(_contest, _a, registration, At(20), Verdict.WrongAnswer),
            Judged(_contest, _a, registration, At(30), Verdict.Accepted)
        };

        var row = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], submissions));

        Assert.Equal(10, row.TotalPenalty);
    }

    [Fact]
    public void Solve_time_uses_received_time_not_judged_time()
    {
        var registration = RegisterStudent(_contest);
        var submission = Submission.Create(_contest, _a, registration, null, registration.StudentUserId!.Value, "C++17", "code", At(15));
        submission.StartProcessing();
        submission.Complete(Verdict.Accepted, 1, 1, At(90));

        var row = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], [submission]));

        Assert.Equal(15, row.TotalPenalty);
    }

    [Fact]
    public void Unsolved_problem_adds_no_penalty()
    {
        var registration = RegisterStudent(_contest);
        var submissions = new[] { Judged(_contest, _a, registration, At(10), Verdict.WrongAnswer) };

        var row = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], submissions));

        Assert.Equal(0, row.SolvedCount);
        Assert.Equal(0, row.TotalPenalty);
        Assert.Equal(1, row.Problems.Single(p => p.ProblemId == _a.Id).PenalizedAttempts);
    }

    [Fact]
    public void Only_completed_submissions_count()
    {
        var registration = RegisterStudent(_contest);
        var pending = Submission.Create(_contest, _a, registration, null, registration.StudentUserId!.Value, "C++17", "code", At(5));
        var cancelled = Submission.Create(_contest, _b, registration, null, registration.StudentUserId!.Value, "C++17", "code", At(6));
        cancelled.Cancel("Excluded", Guid.NewGuid(), At(7));

        var row = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], [pending, cancelled]));

        Assert.Equal(0, row.SolvedCount);
        Assert.All(row.Problems, p => Assert.Equal(0, p.PenalizedAttempts));
    }

    [Fact]
    public void Ranking_orders_by_solved_then_penalty_with_competition_ties()
    {
        var first = RegisterStudent(_contest);
        var tied = RegisterStudent(_contest);
        var third = RegisterStudent(_contest);
        var fourth = RegisterStudent(_contest);
        var submissions = new[]
        {
            Judged(_contest, _a, first, At(10), Verdict.Accepted),
            Judged(_contest, _b, first, At(20), Verdict.Accepted),
            Judged(_contest, _a, tied, At(15), Verdict.Accepted),
            Judged(_contest, _b, tied, At(15), Verdict.Accepted),
            Judged(_contest, _a, third, At(5), Verdict.Accepted)
        };
        var entries = new[] { Entry(fourth, "Dina"), Entry(third, "Chayan"), Entry(tied, "Bashir"), Entry(first, "Anika") };

        var rows = ScoreboardCalculator.CalculateFull(_contest, entries, submissions);

        Assert.Equal(["Anika", "Bashir", "Chayan", "Dina"], rows.Select(r => r.DisplayName));
        Assert.Equal([1, 1, 3, 4], rows.Select(r => r.Rank));
    }

    [Fact]
    public void Display_name_orders_tied_rows_without_changing_rank()
    {
        var zara = RegisterStudent(_contest);
        var amin = RegisterStudent(_contest);

        var rows = ScoreboardCalculator.CalculateFull(_contest, [Entry(zara, "Zara"), Entry(amin, "amin")], []);

        Assert.Equal(["amin", "Zara"], rows.Select(r => r.DisplayName));
        Assert.All(rows, r => Assert.Equal(1, r.Rank));
    }

    [Fact]
    public void Zero_solve_participants_are_included()
    {
        var solver = RegisterStudent(_contest);
        var idle = RegisterStudent(_contest);
        var submissions = new[] { Judged(_contest, _a, solver, At(10), Verdict.Accepted) };

        var rows = ScoreboardCalculator.CalculateFull(_contest, [Entry(idle, "Idle"), Entry(solver, "Solver")], submissions);

        Assert.Equal(2, rows.Count);
        Assert.Equal((2, 0), (rows[1].Rank, rows[1].SolvedCount));
    }

    [Fact]
    public void Public_board_excludes_submissions_received_at_or_after_freeze()
    {
        var registration = RegisterStudent(_contest);
        var submissions = new[]
        {
            Judged(_contest, _a, registration, FreezeAt, Verdict.Accepted),
            Judged(_contest, _b, registration, FreezeAt.AddMinutes(10), Verdict.Accepted)
        };

        var publicRow = Assert.Single(ScoreboardCalculator.CalculatePublic(_contest, [Entry(registration)], submissions));
        var fullRow = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], submissions));

        Assert.Equal(0, publicRow.SolvedCount);
        Assert.Equal(2, fullRow.SolvedCount);
    }

    [Fact]
    public void Public_board_includes_pre_freeze_submission_judged_after_freeze()
    {
        var registration = RegisterStudent(_contest);
        var submission = Submission.Create(_contest, _a, registration, null, registration.StudentUserId!.Value, "C++17", "code", FreezeAt.AddTicks(-1));
        submission.StartProcessing();
        submission.RecordRemoteSubmission("RUN-1");
        submission.Complete(Verdict.Accepted, null, null, FreezeAt.AddMinutes(30));

        var row = Assert.Single(ScoreboardCalculator.CalculatePublic(_contest, [Entry(registration)], [submission]));

        Assert.Equal(1, row.SolvedCount);
    }

    [Fact]
    public void Public_board_without_freeze_shows_all_completed_results()
    {
        var contest = PublishedContest();
        var registration = RegisterStudent(contest);
        var submissions = new[] { Judged(contest, contest.Problems.Single(), registration, EndsAt.AddMinutes(-1), Verdict.Accepted) };

        var row = Assert.Single(ScoreboardCalculator.CalculatePublic(contest, [Entry(registration)], submissions));

        Assert.Equal(1, row.SolvedCount);
    }

    [Fact]
    public void Problem_results_follow_label_order()
    {
        var registration = RegisterStudent(_contest);

        var row = Assert.Single(ScoreboardCalculator.CalculateFull(_contest, [Entry(registration)], []));

        Assert.Equal([_a.Id, _b.Id], row.Problems.Select(p => p.ProblemId));
    }

    private static DateTime At(int minutes, int seconds = 0) => StartsAt.AddMinutes(minutes).AddSeconds(seconds);

    private static ScoreboardEntry Entry(ContestRegistration registration, string name = "Participant") =>
        new(registration.Id, name);
}
