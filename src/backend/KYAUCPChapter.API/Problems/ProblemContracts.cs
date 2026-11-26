using System.ComponentModel.DataAnnotations;
using System.Text;
using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Infrastructure.Persistence;

namespace KYAUCPChapter.API.Problems;

public class InternalProblemRequest
{
    [Required(ErrorMessage = "Enter a label, for example A.")]
    [MaxLength(FieldLengths.ProblemLabel, ErrorMessage = "Use at most 10 characters.")]
    public string Label { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter a title.")]
    [MaxLength(FieldLengths.ProblemTitle, ErrorMessage = "Use at most 200 characters.")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter the statement.")]
    [MaxLength(FieldLengths.ProblemStatement, ErrorMessage = "Use at most 50,000 characters.")]
    public string Statement { get; init; } = string.Empty;

    [Range(Problem.MinimumTimeLimitMs, Problem.MaximumTimeLimitMs, ErrorMessage = "Use 1,000 to 15,000 ms.")]
    public int TimeLimitMs { get; init; }

    [Range(Problem.MinimumMemoryLimitKb, Problem.MaximumMemoryLimitKb, ErrorMessage = "Use 16,384 to 524,288 KB (16 to 512 MB).")]
    public int MemoryLimitKb { get; init; }

    public InternalProblemInput ToInput() => new(Label, Title, Statement, TimeLimitMs, MemoryLimitKb);
}

public sealed class CreateInternalProblemRequest : InternalProblemRequest
{
    public List<TestCaseRequest> TestCases { get; init; } = [];
}

public sealed class TophProblemRequest
{
    [Required(ErrorMessage = "Enter a label, for example A.")]
    [MaxLength(FieldLengths.ProblemLabel, ErrorMessage = "Use at most 10 characters.")]
    public string Label { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter a title.")]
    [MaxLength(FieldLengths.ProblemTitle, ErrorMessage = "Use at most 200 characters.")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter the Toph problem ID.")]
    [MaxLength(FieldLengths.TophProblemId, ErrorMessage = "Use at most 100 characters.")]
    public string ExternalProblemId { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter the Toph problem URL.")]
    [MaxLength(FieldLengths.TophProblemUrl, ErrorMessage = "Use at most 500 characters.")]
    public string ExternalProblemUrl { get; init; } = string.Empty;

    public TophProblemInput ToInput() => new(Label, Title, ExternalProblemId, ExternalProblemUrl);
}

public sealed class TestCaseRequest : IValidatableObject
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "Provide the input, even if it is empty.")]
    public string? Input { get; init; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "Provide the expected output.")]
    public string? ExpectedOutput { get; init; }

    public bool IsSample { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TestCaseLimits.Validate(IsSample, Input ?? string.Empty, ExpectedOutput ?? string.Empty);

    public TestCaseContent ToContent() => new(Input!, ExpectedOutput!, IsSample);
}

public sealed class TestCaseUpdateRequest
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "Provide the input, even if it is empty.")]
    public string? Input { get; init; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "Provide the expected output.")]
    public string? ExpectedOutput { get; init; }
}

public static class TestCaseLimits
{
    public static IEnumerable<ValidationResult> Validate(bool isSample, string input, string expectedOutput)
    {
        foreach (var (name, value) in new[] { ("Input", input), ("ExpectedOutput", expectedOutput) })
        {
            if (isSample && value.Length > FieldLengths.SampleTestText)
            {
                yield return new ValidationResult("Sample text can be at most 10,000 characters.", [name]);
            }
            else if (!isSample && Encoding.UTF8.GetByteCount(value) > FieldLengths.HiddenTestTextBytes)
            {
                yield return new ValidationResult("Hidden test data can be at most 1 MB.", [name]);
            }
        }
    }
}

public sealed record TestCaseResponse(int Position, bool IsSample, string Input, string ExpectedOutput)
{
    public static TestCaseResponse From(TestCase testCase) => new(testCase.Position, testCase.IsSample, testCase.Input, testCase.ExpectedOutput);
}
