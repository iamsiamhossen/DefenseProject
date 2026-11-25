namespace KYAUCPChapter.Core.Domain.Contests;

public enum ContestMode
{
    Individual,
    Team
}

public enum ContestStatus
{
    Draft,
    Published,
    Finalized
}

public enum ContestPhase
{
    Draft,
    Published,
    Running,
    Ended,
    Finalized
}

public enum ProblemType
{
    Internal,
    Toph
}
