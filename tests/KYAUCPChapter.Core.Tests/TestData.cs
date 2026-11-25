using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Submissions;

namespace KYAUCPChapter.Core.Tests;

internal static class TestData
{
    public const int DurationMinutes = 300;

    public static readonly DateTime Now = new(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime OpensAt = Now.AddDays(1);
    public static readonly DateTime ClosesAt = Now.AddDays(2);
    public static readonly DateTime StartsAt = Now.AddDays(3);
    public static readonly DateTime EndsAt = StartsAt.AddMinutes(DurationMinutes);
    public static readonly DateTime FreezeAt = StartsAt.AddMinutes(240);
    public static readonly DateTime DuringRegistration = OpensAt.AddHours(1);
    public static readonly DateTime DuringContest = StartsAt.AddMinutes(30);
    public static readonly DateTime AfterContest = EndsAt.AddMinutes(1);

    public static ContestSchedule Schedule(DateTime? freezeAt = null) =>
        new(OpensAt, ClosesAt, StartsAt, DurationMinutes, freezeAt);

    public static Contest DraftContest(ContestMode mode = ContestMode.Individual, DateTime? freezeAt = null) =>
        Contest.Create("KYAU Monthly Contest", "Monthly practice contest", mode, Schedule(freezeAt), Now);

    public static Contest PublishedContest(ContestMode mode = ContestMode.Individual, DateTime? freezeAt = null)
    {
        var contest = DraftContest(mode, freezeAt);
        AddJudgeableProblem(contest, "A");
        contest.Publish(Now);
        return contest;
    }

    public static readonly TestCaseContent[] OneTestCase = [new("1 2", "3")];

    public static Problem AddInternalProblem(Contest contest, string label, DateTime? now = null) =>
        AddProblem(contest, label, [], now);

    public static Problem AddJudgeableProblem(Contest contest, string label, DateTime? now = null) =>
        AddProblem(contest, label, OneTestCase, now);

    private static Problem AddProblem(Contest contest, string label, TestCaseContent[] testCases, DateTime? now) =>
        contest.AddInternalProblem(label, $"Problem {label}", "Read two integers and print their sum.", 1000, 262144, testCases, now ?? Now);

    public static ContestRegistration RegisterStudent(Contest contest, Guid? userId = null) =>
        ContestRegistration.ForStudent(contest, userId ?? Guid.NewGuid(), DuringRegistration);

    public static Submission Judged(Contest contest, Problem problem, ContestRegistration registration, DateTime receivedAt, Verdict verdict)
    {
        var submission = Submission.Create(contest, problem, registration, null, registration.StudentUserId!.Value, "C++17", "int main() {}", receivedAt);
        submission.StartProcessing();
        submission.Complete(verdict, 15, 1024, receivedAt.AddSeconds(5));
        return submission;
    }
}
