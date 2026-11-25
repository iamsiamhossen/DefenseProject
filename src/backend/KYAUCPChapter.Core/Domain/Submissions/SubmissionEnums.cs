namespace KYAUCPChapter.Core.Domain.Submissions;

public enum SubmissionStatus
{
    Pending,
    Processing,
    AwaitingRemoteVerdict,
    Completed,
    Failed,
    Cancelled
}

public enum Verdict
{
    Accepted,
    WrongAnswer,
    TimeLimitExceeded,
    MemoryLimitExceeded,
    RuntimeError,
    CompilationError
}
