using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Teams;

namespace KYAUCPChapter.Core.Domain.Submissions;

public sealed class Submission
{
    private Submission(Guid registrationId, Guid problemId, Guid submittedByUserId, string language, string sourceCode, DateTime receivedAt)
    {
        Id = Guid.CreateVersion7();
        RegistrationId = registrationId;
        ProblemId = problemId;
        SubmittedByUserId = submittedByUserId;
        Language = language;
        SourceCode = sourceCode;
        ReceivedAt = receivedAt;
        Status = SubmissionStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid RegistrationId { get; private set; }

    public Guid ProblemId { get; private set; }

    public Guid SubmittedByUserId { get; private set; }

    public string Language { get; private set; }

    public string SourceCode { get; private set; }

    public DateTime ReceivedAt { get; private set; }

    public SubmissionStatus Status { get; private set; }

    public string? RemoteSubmissionRef { get; private set; }

    public Verdict? Verdict { get; private set; }

    public int? ExecutionTimeMs { get; private set; }

    public int? MemoryKb { get; private set; }

    public DateTime? JudgedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public string? CancellationReason { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public bool IsResolved => Status is SubmissionStatus.Completed or SubmissionStatus.Cancelled;

    public static Submission Create(
        Contest contest,
        Problem problem,
        ContestRegistration registration,
        Team? team,
        Guid submittedByUserId,
        string language,
        string sourceCode,
        DateTime receivedAt)
    {
        if (!contest.HasProblem(problem.Id))
        {
            throw new DomainException("The problem does not belong to this contest.");
        }

        if (registration.ContestId != contest.Id)
        {
            throw new DomainException("The registration does not belong to this contest.");
        }

        if (!contest.AcceptsSubmissions(receivedAt))
        {
            throw new DomainException("Submissions are only accepted while the contest is running.");
        }

        EnsureSubmitterBelongsToRegistration(registration, team, submittedByUserId);

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new DomainException("Source code is required.");
        }

        return new Submission(registration.Id, problem.Id, submittedByUserId, Guard.Required(language, "Language"), sourceCode, receivedAt);
    }

    public void StartProcessing()
    {
        EnsureStatus(SubmissionStatus.Pending);
        Status = SubmissionStatus.Processing;
    }

    public void RecordRemoteSubmission(string remoteSubmissionRef)
    {
        EnsureStatus(SubmissionStatus.Processing);
        RemoteSubmissionRef = Guard.Required(remoteSubmissionRef, "Remote submission reference");
        Status = SubmissionStatus.AwaitingRemoteVerdict;
    }

    public void Complete(Verdict verdict, int? executionTimeMs, int? memoryKb, DateTime judgedAt)
    {
        EnsureStatus(SubmissionStatus.Processing, SubmissionStatus.AwaitingRemoteVerdict);
        if (executionTimeMs < 0 || memoryKb < 0)
        {
            throw new DomainException("Execution time and memory cannot be negative.");
        }

        Verdict = verdict;
        ExecutionTimeMs = executionTimeMs;
        MemoryKb = memoryKb;
        JudgedAt = judgedAt;
        Status = SubmissionStatus.Completed;
    }

    public void Fail(string reason)
    {
        EnsureStatus(SubmissionStatus.Processing, SubmissionStatus.AwaitingRemoteVerdict);
        FailureReason = Guard.Required(reason, "Failure reason");
        Status = SubmissionStatus.Failed;
    }

    public void Retry()
    {
        EnsureStatus(SubmissionStatus.Failed);
        FailureReason = null;
        Status = RemoteSubmissionRef is null ? SubmissionStatus.Pending : SubmissionStatus.AwaitingRemoteVerdict;
    }

    public void Cancel(string reason, Guid cancelledByUserId, DateTime cancelledAt)
    {
        if (IsResolved)
        {
            throw new DomainException($"A {Status} submission cannot be cancelled.");
        }

        CancellationReason = Guard.Required(reason, "Cancellation reason");
        CancelledByUserId = cancelledByUserId;
        CancelledAt = cancelledAt;
        Status = SubmissionStatus.Cancelled;
    }

    private static void EnsureSubmitterBelongsToRegistration(ContestRegistration registration, Team? team, Guid userId)
    {
        if (registration.TeamId is null)
        {
            if (registration.StudentUserId != userId)
            {
                throw new DomainException("The student is not registered for this contest.");
            }

            return;
        }

        if (team is null || team.Id != registration.TeamId)
        {
            throw new ArgumentException("The registration's team is required for a team submission.", nameof(team));
        }

        if (!team.HasMember(userId))
        {
            throw new DomainException("The student is not a member of the registered team.");
        }
    }

    private void EnsureStatus(params SubmissionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException($"This action is not allowed for a {Status} submission.");
        }
    }
}
