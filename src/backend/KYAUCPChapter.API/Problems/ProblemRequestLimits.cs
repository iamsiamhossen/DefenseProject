using KYAUCPChapter.Core.Common;

namespace KYAUCPChapter.API.Problems;

public static class ProblemRequestLimits
{
    public const int MaximumJsonEscapeExpansion = 6;
    public const int EnvelopeAllowanceBytes = 1_048_576;

    public const long TestCaseRequestBytes =
        2L * FieldLengths.HiddenTestTextBytes * MaximumJsonEscapeExpansion + EnvelopeAllowanceBytes;
}
