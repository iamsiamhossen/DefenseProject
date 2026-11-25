namespace KYAUCPChapter.Core.Domain.Contests;

public sealed class TestCase
{
    internal TestCase(Guid problemId, int position, string input, string expectedOutput)
    {
        ProblemId = problemId;
        Position = position;
        Input = input;
        ExpectedOutput = expectedOutput;
    }

    public Guid ProblemId { get; private set; }

    public int Position { get; private set; }

    public string Input { get; private set; }

    public string ExpectedOutput { get; private set; }

    internal void Update(string input, string expectedOutput)
    {
        Input = input;
        ExpectedOutput = expectedOutput;
    }
}
