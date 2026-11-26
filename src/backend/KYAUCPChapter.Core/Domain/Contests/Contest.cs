using KYAUCPChapter.Core.Common;

namespace KYAUCPChapter.Core.Domain.Contests;

public sealed class Contest
{
    private readonly List<Problem> _problems = [];

    private Contest(string title, string description, ContestMode mode)
    {
        Id = Guid.CreateVersion7();
        Title = title;
        Description = description;
        Mode = mode;
        Status = ContestStatus.Draft;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public ContestMode Mode { get; private set; }

    public ContestStatus Status { get; private set; }

    public ContestSchedule Schedule { get; private set; } = null!;

    public DateTime? FinalizedAt { get; private set; }

    public IReadOnlyList<Problem> Problems => _problems.AsReadOnly();

    public static Contest Create(string title, string? description, ContestMode mode, ContestSchedule schedule, DateTime now)
    {
        if (schedule.RegistrationOpensAt < now)
        {
            throw new DomainException("Contest timestamps cannot be set in the past.");
        }

        return new Contest(Guard.Required(title, "Contest title"), description?.Trim() ?? string.Empty, mode)
        {
            Schedule = schedule
        };
    }

    public ContestPhase GetPhase(DateTime now) => Status switch
    {
        ContestStatus.Draft => ContestPhase.Draft,
        ContestStatus.Finalized => ContestPhase.Finalized,
        _ when now < Schedule.StartsAt => ContestPhase.Published,
        _ when now < Schedule.EndsAt => ContestPhase.Running,
        _ => ContestPhase.Ended
    };

    public bool IsRegistrationOpen(DateTime now) =>
        Status == ContestStatus.Published
        && Schedule.RegistrationOpensAt <= now
        && now < Schedule.RegistrationClosesAt;

    public bool AcceptsSubmissions(DateTime now) => GetPhase(now) == ContestPhase.Running;

    public bool AreProblemsVisibleToParticipants(DateTime now) =>
        GetPhase(now) is ContestPhase.Running or ContestPhase.Ended or ContestPhase.Finalized;

    public void UpdateDetails(string title, string? description, DateTime now)
    {
        EnsureEditable(now);
        Title = Guard.Required(title, "Contest title");
        Description = description?.Trim() ?? string.Empty;
    }

    public void ChangeMode(ContestMode mode, bool hasRegistrations, DateTime now)
    {
        EnsureEditable(now);
        if (mode != Mode && hasRegistrations)
        {
            throw new DomainException("Contest mode cannot be changed after a registration exists.");
        }

        Mode = mode;
    }

    public void Reschedule(ContestSchedule schedule, DateTime now)
    {
        EnsureEditable(now);
        EnsureTimestampChange(Schedule.RegistrationOpensAt, schedule.RegistrationOpensAt, now, "Registration opening time");
        EnsureTimestampChange(Schedule.RegistrationClosesAt, schedule.RegistrationClosesAt, now, "Registration closing time");
        EnsureTimestampChange(Schedule.StartsAt, schedule.StartsAt, now, "Contest start time");
        EnsureTimestampChange(Schedule.FreezeAt, schedule.FreezeAt, now, "Freeze time");
        Schedule = schedule;
    }

    public void Publish(DateTime now)
    {
        if (Status != ContestStatus.Draft)
        {
            throw new DomainException("Only a draft contest can be published.");
        }

        if (Schedule.RegistrationOpensAt <= now)
        {
            throw new DomainException("Registration opening, registration closing and start times must be in the future to publish.");
        }

        if (_problems.Count == 0)
        {
            throw new DomainException("A contest needs at least one problem before it can be published.");
        }

        if (_problems.Any(p => p.Type == ProblemType.Internal && !p.HasHiddenTestCase))
        {
            throw new DomainException("Every internal problem needs at least one hidden test case before publication.");
        }

        Status = ContestStatus.Published;
    }

    public void EnsureCanBeDeleted()
    {
        if (Status != ContestStatus.Draft)
        {
            throw new DomainException("Only a draft contest can be deleted.");
        }
    }

    public Problem AddInternalProblem(
        string label,
        string title,
        string statement,
        int timeLimitMs,
        int memoryLimitKb,
        IReadOnlyCollection<TestCaseContent> testCases,
        DateTime now)
    {
        EnsureEditable(now);
        if (Status == ContestStatus.Published && testCases.All(t => t.IsSample))
        {
            throw new DomainException("An internal problem added to a published contest needs at least one hidden test case.");
        }

        var problem = Problem.CreateInternal(Id, UniqueLabel(label, null), title, statement, timeLimitMs, memoryLimitKb);
        foreach (var testCase in testCases)
        {
            problem.AddTestCase(testCase.Input, testCase.ExpectedOutput, testCase.IsSample);
        }

        _problems.Add(problem);
        return problem;
    }

    public Problem AddTophProblem(string label, string title, string externalProblemId, string externalProblemUrl, DateTime now)
    {
        EnsureEditable(now);
        var problem = Problem.CreateToph(Id, UniqueLabel(label, null), title, externalProblemId, externalProblemUrl);
        _problems.Add(problem);
        return problem;
    }

    public void UpdateInternalProblem(Guid problemId, string label, string title, string statement, int timeLimitMs, int memoryLimitKb, DateTime now)
    {
        EnsureEditable(now);
        GetProblem(problemId).UpdateInternal(UniqueLabel(label, problemId), title, statement, timeLimitMs, memoryLimitKb);
    }

    public void UpdateTophProblem(Guid problemId, string label, string title, string externalProblemId, string externalProblemUrl, DateTime now)
    {
        EnsureEditable(now);
        GetProblem(problemId).UpdateToph(UniqueLabel(label, problemId), title, externalProblemId, externalProblemUrl);
    }

    public void RemoveProblem(Guid problemId, DateTime now)
    {
        EnsureEditable(now);
        var problem = GetProblem(problemId);
        if (Status == ContestStatus.Published && _problems.Count == 1)
        {
            throw new DomainException("A published contest must keep at least one problem.");
        }

        _problems.Remove(problem);
    }

    public TestCase AddTestCase(Guid problemId, string input, string expectedOutput, bool isSample, DateTime now)
    {
        EnsureEditable(now);
        return GetProblem(problemId).AddTestCase(input, expectedOutput, isSample);
    }

    public void UpdateTestCase(Guid problemId, int position, string input, string expectedOutput, DateTime now)
    {
        EnsureEditable(now);
        GetProblem(problemId).UpdateTestCase(position, input, expectedOutput);
    }

    public void RemoveTestCase(Guid problemId, int position, DateTime now)
    {
        EnsureEditable(now);
        GetProblem(problemId).RemoveTestCase(position, keepAHiddenTestCase: Status == ContestStatus.Published);
    }

    public bool HasProblem(Guid problemId) => _problems.Any(p => p.Id == problemId);

    internal void MarkFinalized(DateTime now)
    {
        Status = ContestStatus.Finalized;
        FinalizedAt = now;
    }

    private void EnsureEditable(DateTime now)
    {
        var phase = GetPhase(now);
        if (phase != ContestPhase.Draft && phase != ContestPhase.Published)
        {
            throw new DomainException("A contest cannot be edited after it starts.");
        }
    }

    private Problem GetProblem(Guid problemId) =>
        _problems.SingleOrDefault(p => p.Id == problemId)
        ?? throw new DomainException("Problem was not found in this contest.");

    private string UniqueLabel(string label, Guid? problemId)
    {
        var trimmed = Guard.Required(label, "Problem label");
        if (_problems.Any(p => p.Id != problemId && string.Equals(p.Label, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Problem label '{trimmed}' is already used in this contest.");
        }

        return trimmed;
    }

    private static void EnsureTimestampChange(DateTime? current, DateTime? proposed, DateTime now, string name)
    {
        if (current == proposed)
        {
            return;
        }

        if (current <= now)
        {
            throw new DomainException($"{name} has already passed and cannot be changed.");
        }

        if (proposed < now)
        {
            throw new DomainException($"{name} cannot be set in the past.");
        }
    }
}
