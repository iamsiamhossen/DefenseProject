namespace KYAUCPChapter.Core.Domain.Scoring;

public sealed class FinalStanding
{
    private FinalStanding(Guid registrationId, int rank, int solvedCount, int totalPenalty, string displayName)
    {
        RegistrationId = registrationId;
        Rank = rank;
        SolvedCount = solvedCount;
        TotalPenalty = totalPenalty;
        DisplayName = displayName;
    }

    public Guid RegistrationId { get; private set; }

    public int Rank { get; private set; }

    public int SolvedCount { get; private set; }

    public int TotalPenalty { get; private set; }

    public string DisplayName { get; private set; }

    internal static FinalStanding From(ScoreboardRow row) =>
        new(row.RegistrationId, row.Rank, row.SolvedCount, row.TotalPenalty, row.DisplayName);
}
