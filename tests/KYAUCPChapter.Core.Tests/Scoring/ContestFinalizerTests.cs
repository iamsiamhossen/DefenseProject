using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Scoring;
using KYAUCPChapter.Core.Domain.Submissions;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Scoring;

public class ContestFinalizerTests
{
    private readonly Contest _contest = PublishedContest(freezeAt: FreezeAt);
    private readonly ContestRegistration _solver;
    private readonly ContestRegistration _idle;

    public ContestFinalizerTests()
    {
        _solver = RegisterStudent(_contest);
        _idle = RegisterStudent(_contest);
    }

    [Fact]
    public void Finalization_writes_full_standings_for_every_registration()
    {
        var afterFreeze = Judged(_contest, _contest.Problems.Single(), _solver, FreezeAt.AddMinutes(5), Verdict.Accepted);

        var standings = ContestFinalizer.Finalize(_contest, Entries(), [afterFreeze], AfterContest);

        Assert.Equal(2, standings.Count);
        var top = standings[0];
        Assert.Equal((_solver.Id, 1, 1, 245, "Solver"), (top.RegistrationId, top.Rank, top.SolvedCount, top.TotalPenalty, top.DisplayName));
        Assert.Equal((_idle.Id, 2, 0, 0), (standings[1].RegistrationId, standings[1].Rank, standings[1].SolvedCount, standings[1].TotalPenalty));
        Assert.Equal(ContestStatus.Finalized, _contest.Status);
        Assert.Equal(AfterContest, _contest.FinalizedAt);
        Assert.Equal(ContestPhase.Finalized, _contest.GetPhase(AfterContest));
    }

    [Fact]
    public void Cancelled_submissions_do_not_block_finalization()
    {
        var cancelled = Submit();
        cancelled.Cancel("Excluded by organizer", Guid.NewGuid(), AfterContest);

        var standings = ContestFinalizer.Finalize(_contest, Entries(), [cancelled], AfterContest);

        Assert.All(standings, s => Assert.Equal(0, s.SolvedCount));
    }

    [Theory]
    [InlineData(SubmissionStatus.Pending)]
    [InlineData(SubmissionStatus.Processing)]
    [InlineData(SubmissionStatus.AwaitingRemoteVerdict)]
    [InlineData(SubmissionStatus.Failed)]
    public void Unresolved_submissions_block_finalization(SubmissionStatus status)
    {
        var submission = InStatus(status);

        Assert.Throws<DomainException>(() => ContestFinalizer.Finalize(_contest, Entries(), [submission], AfterContest));
        Assert.Equal(ContestStatus.Published, _contest.Status);
        Assert.Null(_contest.FinalizedAt);
    }

    [Fact]
    public void Contest_must_have_ended()
    {
        Assert.Throws<DomainException>(() => ContestFinalizer.Finalize(_contest, Entries(), [], EndsAt.AddTicks(-1)));
    }

    [Fact]
    public void Contest_cannot_be_finalized_twice()
    {
        ContestFinalizer.Finalize(_contest, Entries(), [], AfterContest);

        Assert.Throws<DomainException>(() => ContestFinalizer.Finalize(_contest, Entries(), [], AfterContest));
    }

    [Fact]
    public void Draft_contest_cannot_be_finalized()
    {
        Assert.Throws<DomainException>(() => ContestFinalizer.Finalize(DraftContest(), [], [], AfterContest));
    }

    private ScoreboardEntry[] Entries() => [new(_solver.Id, "Solver"), new(_idle.Id, "Idle")];

    private Submission Submit() =>
        Submission.Create(_contest, _contest.Problems.Single(), _solver, null, _solver.StudentUserId!.Value, "C++17", "code", DuringContest);

    private Submission InStatus(SubmissionStatus status)
    {
        var submission = Submit();
        if (status == SubmissionStatus.Pending)
        {
            return submission;
        }

        submission.StartProcessing();
        if (status == SubmissionStatus.AwaitingRemoteVerdict)
        {
            submission.RecordRemoteSubmission("RUN-1");
        }
        else if (status == SubmissionStatus.Failed)
        {
            submission.Fail("Judge0 unavailable");
        }

        return submission;
    }
}
