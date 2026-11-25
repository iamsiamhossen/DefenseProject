using KYAUCPChapter.Core.Common;

namespace KYAUCPChapter.Core.Domain.Contests;

public sealed class Problem
{
    private readonly List<TestCase> _testCases = [];

    private Problem(Guid contestId, ProblemType type)
    {
        Id = Guid.CreateVersion7();
        ContestId = contestId;
        Type = type;
    }

    public Guid Id { get; private set; }

    public Guid ContestId { get; private set; }

    public ProblemType Type { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Statement { get; private set; }

    public int? TimeLimitMs { get; private set; }

    public int? MemoryLimitKb { get; private set; }

    public string? ExternalProblemId { get; private set; }

    public string? ExternalProblemUrl { get; private set; }

    public IReadOnlyList<TestCase> TestCases => _testCases.AsReadOnly();

    internal static Problem CreateInternal(
        Guid contestId,
        string label,
        string title,
        string statement,
        int timeLimitMs,
        int memoryLimitKb)
    {
        var problem = new Problem(contestId, ProblemType.Internal);
        problem.UpdateInternal(label, title, statement, timeLimitMs, memoryLimitKb);
        return problem;
    }

    internal static Problem CreateToph(
        Guid contestId,
        string label,
        string title,
        string externalProblemId,
        string externalProblemUrl)
    {
        var problem = new Problem(contestId, ProblemType.Toph);
        problem.UpdateToph(label, title, externalProblemId, externalProblemUrl);
        return problem;
    }

    internal void UpdateInternal(string label, string title, string statement, int timeLimitMs, int memoryLimitKb)
    {
        EnsureType(ProblemType.Internal);
        Label = label;
        Title = Guard.Required(title, "Problem title");
        Statement = Guard.Required(statement, "Problem statement");
        TimeLimitMs = Guard.Positive(timeLimitMs, "Time limit");
        MemoryLimitKb = Guard.Positive(memoryLimitKb, "Memory limit");
    }

    internal void UpdateToph(string label, string title, string externalProblemId, string externalProblemUrl)
    {
        EnsureType(ProblemType.Toph);
        var url = Guard.Required(externalProblemUrl, "Toph problem URL");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new DomainException("Toph problem URL must be an absolute HTTP or HTTPS URL.");
        }

        Label = label;
        Title = Guard.Required(title, "Problem title");
        ExternalProblemId = Guard.Required(externalProblemId, "Toph problem ID");
        ExternalProblemUrl = url;
    }

    internal TestCase AddTestCase(string input, string expectedOutput)
    {
        EnsureType(ProblemType.Internal);
        var position = _testCases.Count == 0 ? 1 : _testCases.Max(t => t.Position) + 1;
        var testCase = new TestCase(Id, position, input, expectedOutput);
        _testCases.Add(testCase);
        return testCase;
    }

    internal void UpdateTestCase(int position, string input, string expectedOutput) =>
        GetTestCase(position).Update(input, expectedOutput);

    internal void RemoveTestCase(int position, bool keepAtLeastOne)
    {
        var testCase = GetTestCase(position);
        if (keepAtLeastOne && _testCases.Count == 1)
        {
            throw new DomainException("An internal problem in a published contest must keep at least one hidden test case.");
        }

        _testCases.Remove(testCase);
    }

    private TestCase GetTestCase(int position) =>
        _testCases.SingleOrDefault(t => t.Position == position)
        ?? throw new DomainException("Test case was not found.");

    private void EnsureType(ProblemType type)
    {
        if (Type != type)
        {
            throw new DomainException($"This operation is only available for {type} problems.");
        }
    }
}
