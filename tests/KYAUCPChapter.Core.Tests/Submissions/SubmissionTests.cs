using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Submissions;
using KYAUCPChapter.Core.Domain.Teams;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Submissions;

public class SubmissionTests
{
    private readonly Contest _contest = PublishedContest();
    private readonly Guid _student = Guid.NewGuid();
    private readonly ContestRegistration _registration;

    public SubmissionTests()
    {
        _registration = RegisterStudent(_contest, _student);
    }

    [Fact]
    public void New_submission_is_pending_with_server_received_time()
    {
        var submission = Submit();

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Equal(DuringContest, submission.ReceivedAt);
        Assert.Equal(_student, submission.SubmittedByUserId);
        Assert.Equal(_registration.Id, submission.RegistrationId);
        Assert.False(submission.IsResolved);
    }

    [Theory]
    [MemberData(nameof(OutsideContest))]
    public void Submissions_are_rejected_outside_running_phase(DateTime receivedAt)
    {
        Assert.Throws<DomainException>(() => Submit(receivedAt: receivedAt));
    }

    public static TheoryData<DateTime> OutsideContest => new() { StartsAt.AddTicks(-1), EndsAt };

    [Fact]
    public void Problem_must_belong_to_the_contest()
    {
        var otherProblem = AddJudgeableProblem(DraftContest(), "A");

        Assert.Throws<DomainException>(() => Submission.Create(_contest, otherProblem, _registration, null, _student, "C++17", "code", DuringContest));
    }

    [Fact]
    public void Registration_must_belong_to_the_contest()
    {
        var other = PublishedContest();
        var otherRegistration = RegisterStudent(other, _student);

        Assert.Throws<DomainException>(() => Submission.Create(_contest, _contest.Problems.Single(), otherRegistration, null, _student, "C++17", "code", DuringContest));
    }

    [Fact]
    public void Individual_submitter_must_be_the_registered_student()
    {
        Assert.Throws<DomainException>(() => Submit(userId: Guid.NewGuid()));
    }

    [Theory]
    [InlineData("C++17", " ")]
    [InlineData(" ", "int main() {}")]
    public void Language_and_source_code_are_required(string language, string sourceCode)
    {
        Assert.Throws<DomainException>(() => Submission.Create(_contest, _contest.Problems.Single(), _registration, null, _student, language, sourceCode, DuringContest));
    }

    [Fact]
    public void Team_submission_records_the_actual_member()
    {
        var contest = PublishedContest(ContestMode.Team);
        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        var team = Team.Create(contest, "Team", leader, DuringRegistration);
        team.Join(contest, member, DuringRegistration);
        var registration = ContestRegistration.ForTeam(contest, team, DuringRegistration);

        var submission = Submission.Create(contest, contest.Problems.Single(), registration, team, member, "C++17", "code", DuringContest);

        Assert.Equal(member, submission.SubmittedByUserId);
        Assert.Equal(registration.Id, submission.RegistrationId);
    }

    [Fact]
    public void Team_submitter_must_be_a_member()
    {
        var contest = PublishedContest(ContestMode.Team);
        var team = Team.Create(contest, "Team", Guid.NewGuid(), DuringRegistration);
        var registration = ContestRegistration.ForTeam(contest, team, DuringRegistration);

        Assert.Throws<DomainException>(() => Submission.Create(contest, contest.Problems.Single(), registration, team, Guid.NewGuid(), "C++17", "code", DuringContest));
    }

    [Fact]
    public void Team_submission_requires_the_registered_team()
    {
        var contest = PublishedContest(ContestMode.Team);
        var leader = Guid.NewGuid();
        var team = Team.Create(contest, "Team", leader, DuringRegistration);
        var otherTeam = Team.Create(contest, "Other", leader, DuringRegistration);
        var registration = ContestRegistration.ForTeam(contest, team, DuringRegistration);

        Assert.Throws<ArgumentException>(() => Submission.Create(contest, contest.Problems.Single(), registration, null, leader, "C++17", "code", DuringContest));
        Assert.Throws<ArgumentException>(() => Submission.Create(contest, contest.Problems.Single(), registration, otherTeam, leader, "C++17", "code", DuringContest));
    }

    [Fact]
    public void Internal_judging_completes_with_verdict_and_metrics()
    {
        var submission = Submit();
        var judgedAt = DuringContest.AddSeconds(3);

        submission.StartProcessing();
        submission.Complete(Verdict.WrongAnswer, 120, 2048, judgedAt);

        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        Assert.Equal(Verdict.WrongAnswer, submission.Verdict);
        Assert.Equal(120, submission.ExecutionTimeMs);
        Assert.Equal(2048, submission.MemoryKb);
        Assert.Equal(judgedAt, submission.JudgedAt);
        Assert.True(submission.IsResolved);
    }

    [Fact]
    public void Remote_judging_waits_for_verdict_then_completes()
    {
        var submission = Submit();

        submission.StartProcessing();
        submission.RecordRemoteSubmission("RUN-123");
        Assert.Equal(SubmissionStatus.AwaitingRemoteVerdict, submission.Status);

        submission.Complete(Verdict.Accepted, null, null, DuringContest.AddMinutes(1));

        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        Assert.Equal("RUN-123", submission.RemoteSubmissionRef);
    }

    [Fact]
    public void Negative_metrics_are_rejected()
    {
        var submission = Submit();
        submission.StartProcessing();

        Assert.Throws<DomainException>(() => submission.Complete(Verdict.Accepted, -1, 0, DuringContest));
    }

    [Fact]
    public void Pending_submission_cannot_complete()
    {
        var submission = Submit();

        Assert.Throws<DomainException>(() => submission.Complete(Verdict.Accepted, 1, 1, DuringContest));
    }

    [Fact]
    public void Failure_stores_reason_without_a_verdict()
    {
        var submission = Submit();
        submission.StartProcessing();

        submission.Fail("Judge0 unavailable");

        Assert.Equal(SubmissionStatus.Failed, submission.Status);
        Assert.Equal("Judge0 unavailable", submission.FailureReason);
        Assert.Null(submission.Verdict);
        Assert.False(submission.IsResolved);
    }

    [Fact]
    public void Retry_without_remote_reference_returns_to_pending()
    {
        var submission = Submit();
        submission.StartProcessing();
        submission.Fail("Judge0 unavailable");

        submission.Retry();

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Null(submission.FailureReason);
    }

    [Fact]
    public void Retry_with_remote_reference_resumes_polling()
    {
        var submission = Submit();
        submission.StartProcessing();
        submission.RecordRemoteSubmission("RUN-123");
        submission.Fail("Polling timed out");

        submission.Retry();

        Assert.Equal(SubmissionStatus.AwaitingRemoteVerdict, submission.Status);
        Assert.Equal("RUN-123", submission.RemoteSubmissionRef);
    }

    [Fact]
    public void Only_failed_submission_can_be_retried()
    {
        var submission = Submit();

        Assert.Throws<DomainException>(submission.Retry);
    }

    [Fact]
    public void Cancellation_requires_a_reason_and_records_the_actor()
    {
        var submission = Submit();
        var organizer = Guid.NewGuid();
        var cancelledAt = DuringContest.AddMinutes(5);

        Assert.Throws<DomainException>(() => submission.Cancel(" ", organizer, cancelledAt));
        submission.Cancel("Duplicate remote run", organizer, cancelledAt);

        Assert.Equal(SubmissionStatus.Cancelled, submission.Status);
        Assert.Equal("Duplicate remote run", submission.CancellationReason);
        Assert.Equal(organizer, submission.CancelledByUserId);
        Assert.Equal(cancelledAt, submission.CancelledAt);
        Assert.True(submission.IsResolved);
    }

    [Fact]
    public void Pending_submission_cannot_fail()
    {
        var submission = Submit();

        Assert.Throws<DomainException>(() => submission.Fail("Judge0 unavailable"));
    }

    [Fact]
    public void Failed_submission_can_be_cancelled()
    {
        var submission = Submit();
        submission.StartProcessing();
        submission.Fail("Judge0 unavailable");

        submission.Cancel("Problem misconfigured", Guid.NewGuid(), AfterContest);

        Assert.Equal(SubmissionStatus.Cancelled, submission.Status);
    }

    [Fact]
    public void Terminal_submissions_cannot_change()
    {
        var completed = Submit();
        completed.StartProcessing();
        completed.Complete(Verdict.Accepted, 1, 1, DuringContest);
        var cancelled = Submit();
        cancelled.Cancel("Excluded", Guid.NewGuid(), DuringContest);

        Assert.Throws<DomainException>(() => completed.Cancel("Excluded", Guid.NewGuid(), DuringContest));
        Assert.Throws<DomainException>(() => cancelled.Cancel("Again", Guid.NewGuid(), DuringContest));
        Assert.Throws<DomainException>(completed.StartProcessing);
        Assert.Throws<DomainException>(cancelled.StartProcessing);
    }

    private Submission Submit(Guid? userId = null, DateTime? receivedAt = null) =>
        Submission.Create(_contest, _contest.Problems.Single(), _registration, null, userId ?? _student, "C++17", "int main() {}", receivedAt ?? DuringContest);
}
