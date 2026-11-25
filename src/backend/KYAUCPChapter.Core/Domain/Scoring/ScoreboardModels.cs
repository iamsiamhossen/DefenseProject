namespace KYAUCPChapter.Core.Domain.Scoring;

public sealed record ScoreboardEntry(Guid RegistrationId, string DisplayName);

public sealed record ProblemResult(Guid ProblemId, bool IsSolved, int PenalizedAttempts, int? SolvedAtMinute);

public sealed record ScoreboardRow(
    int Rank,
    Guid RegistrationId,
    string DisplayName,
    int SolvedCount,
    int TotalPenalty,
    IReadOnlyList<ProblemResult> Problems);
