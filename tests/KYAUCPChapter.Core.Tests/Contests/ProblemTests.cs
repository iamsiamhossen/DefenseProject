using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Contests;

public class ProblemTests
{
    private const string TophUrl = "https://toph.co/p/add-them-up";

    [Fact]
    public void Internal_problem_stores_content_and_limits()
    {
        var contest = DraftContest();

        var problem = contest.AddInternalProblem(" A ", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3"), new("5 5", "10")], Now);

        Assert.Equal(ProblemType.Internal, problem.Type);
        Assert.Equal("A", problem.Label);
        Assert.Equal(contest.Id, problem.ContestId);
        Assert.Equal(1000, problem.TimeLimitMs);
        Assert.Equal(262144, problem.MemoryLimitKb);
        Assert.Null(problem.ExternalProblemId);
        Assert.Equal([(1, "3"), (2, "10")], problem.TestCases.Select(t => (t.Position, t.ExpectedOutput)));
    }

    [Theory]
    [InlineData(1000, 16384)]
    [InlineData(15000, 524288)]
    public void Internal_problem_limits_accept_the_approved_boundaries(int timeLimitMs, int memoryLimitKb)
    {
        var problem = DraftContest().AddInternalProblem("A", "Sum", "Print a + b.", timeLimitMs, memoryLimitKb, [], Now);

        Assert.Equal((timeLimitMs, memoryLimitKb), (problem.TimeLimitMs, problem.MemoryLimitKb));
    }

    [Theory]
    [InlineData(999, 262144)]
    [InlineData(15001, 262144)]
    [InlineData(1000, 16383)]
    [InlineData(1000, 524289)]
    [InlineData(0, 262144)]
    public void Internal_problem_limits_must_be_within_the_approved_ranges(int timeLimitMs, int memoryLimitKb)
    {
        var contest = DraftContest();

        Assert.Throws<DomainException>(() => contest.AddInternalProblem("A", "Sum", "Print a + b.", timeLimitMs, memoryLimitKb, OneTestCase, Now));
    }

    [Fact]
    public void Sample_test_cases_do_not_count_as_hidden()
    {
        var contest = DraftContest();
        var problem = contest.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3", IsSample: true)], Now);

        Assert.True(problem.TestCases.Single().IsSample);
        Assert.False(problem.HasHiddenTestCase);
        Assert.Throws<DomainException>(() => contest.Publish(Now));
    }

    [Fact]
    public void Published_contest_rejects_a_new_internal_problem_with_only_samples()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() =>
            contest.AddInternalProblem("B", "Product", "Print a * b.", 1000, 262144, [new("2 3", "6", IsSample: true)], DuringRegistration));
    }

    [Fact]
    public void Published_contest_can_remove_samples_but_must_keep_a_hidden_test_case()
    {
        var contest = PublishedContest();
        var problem = contest.Problems.Single();
        var sample = contest.AddTestCase(problem.Id, "5 5", "10", isSample: true, DuringRegistration);

        contest.RemoveTestCase(problem.Id, sample.Position, DuringRegistration);
        Assert.Throws<DomainException>(() => contest.RemoveTestCase(problem.Id, 1, DuringRegistration));
    }

    [Fact]
    public void Samples_and_hidden_cases_share_one_position_sequence()
    {
        var contest = DraftContest();
        var problem = AddInternalProblem(contest, "A");

        var hidden = contest.AddTestCase(problem.Id, "1 2", "3", isSample: false, Now);
        var sample = contest.AddTestCase(problem.Id, "2 2", "4", isSample: true, Now);

        Assert.Equal((1, false, 2, true), (hidden.Position, hidden.IsSample, sample.Position, sample.IsSample));
    }

    [Theory]
    [InlineData(0, 262144)]
    [InlineData(1000, 0)]
    public void Internal_problem_limits_must_be_positive(int timeLimitMs, int memoryLimitKb)
    {
        var contest = DraftContest();

        Assert.Throws<DomainException>(() => contest.AddInternalProblem("A", "Sum", "Print a + b.", timeLimitMs, memoryLimitKb, OneTestCase, Now));
    }

    [Fact]
    public void Toph_problem_stores_only_reference_data()
    {
        var contest = DraftContest();

        var problem = contest.AddTophProblem("B", "Add Them Up", "add-them-up", TophUrl, Now);

        Assert.Equal(ProblemType.Toph, problem.Type);
        Assert.Equal("add-them-up", problem.ExternalProblemId);
        Assert.Equal(TophUrl, problem.ExternalProblemUrl);
        Assert.Null(problem.Statement);
        Assert.Null(problem.TimeLimitMs);
    }

    [Theory]
    [InlineData("toph.co/p/add-them-up")]
    [InlineData("ftp://toph.co/p/add-them-up")]
    public void Toph_problem_url_must_be_absolute_http(string url)
    {
        var contest = DraftContest();

        Assert.Throws<DomainException>(() => contest.AddTophProblem("B", "Add Them Up", "add-them-up", url, Now));
    }

    [Fact]
    public void Toph_problem_reference_can_be_updated()
    {
        var contest = DraftContest();
        var problem = contest.AddTophProblem("B", "Add Them Up", "add-them-up", TophUrl, Now);

        contest.UpdateTophProblem(problem.Id, "C", "Renamed", "renamed-id", "https://toph.co/p/renamed-id", Now);

        Assert.Equal(("C", "Renamed", "renamed-id"), (problem.Label, problem.Title, problem.ExternalProblemId));
    }

    [Fact]
    public void Problem_labels_are_unique_ignoring_case()
    {
        var contest = DraftContest();
        AddInternalProblem(contest, "A");

        Assert.Throws<DomainException>(() => AddInternalProblem(contest, "a"));
    }

    [Fact]
    public void Problem_can_keep_its_own_label_when_updated()
    {
        var contest = DraftContest();
        var problem = AddInternalProblem(contest, "A");

        contest.UpdateInternalProblem(problem.Id, "A", "New title", "New statement", 2000, 131072, Now);

        Assert.Equal("New title", problem.Title);
        Assert.Equal(2000, problem.TimeLimitMs);
    }

    [Fact]
    public void Problem_cannot_take_another_problems_label()
    {
        var contest = DraftContest();
        AddInternalProblem(contest, "A");
        var second = AddInternalProblem(contest, "B");

        Assert.Throws<DomainException>(() => contest.UpdateInternalProblem(second.Id, "A", "Title", "Statement", 1000, 262144, Now));
    }

    [Fact]
    public void Problem_type_specific_updates_are_enforced()
    {
        var contest = DraftContest();
        var toph = contest.AddTophProblem("B", "Add Them Up", "add-them-up", TophUrl, Now);

        Assert.Throws<DomainException>(() => contest.UpdateInternalProblem(toph.Id, "B", "Title", "Statement", 1000, 262144, Now));
    }

    [Fact]
    public void Toph_problem_cannot_have_test_cases()
    {
        var contest = DraftContest();
        var toph = contest.AddTophProblem("B", "Add Them Up", "add-them-up", TophUrl, Now);

        Assert.Throws<DomainException>(() => contest.AddTestCase(toph.Id, "1 2", "3", isSample: false, Now));
    }

    [Fact]
    public void Test_case_positions_increase_and_are_not_reused()
    {
        var contest = DraftContest();
        var problem = AddInternalProblem(contest, "A");

        contest.AddTestCase(problem.Id, "1 2", "3", isSample: false, Now);
        contest.AddTestCase(problem.Id, "2 2", "4", isSample: false, Now);
        contest.RemoveTestCase(problem.Id, 1, Now);
        var third = contest.AddTestCase(problem.Id, "5 5", "10", isSample: false, Now);

        Assert.Equal(3, third.Position);
        Assert.Equal([2, 3], problem.TestCases.Select(t => t.Position));
    }

    [Fact]
    public void Test_case_can_be_updated()
    {
        var contest = DraftContest();
        var problem = AddInternalProblem(contest, "A");
        contest.AddTestCase(problem.Id, "1 2", "3", isSample: false, Now);

        contest.UpdateTestCase(problem.Id, 1, "4 4", "8", Now);

        Assert.Equal("8", problem.TestCases.Single().ExpectedOutput);
    }

    [Fact]
    public void Test_cases_are_locked_after_start()
    {
        var contest = PublishedContest();
        var problem = contest.Problems.Single();

        Assert.Throws<DomainException>(() => contest.AddTestCase(problem.Id, "1 2", "3", isSample: false, StartsAt));
    }

    [Fact]
    public void Internal_problem_added_to_published_contest_needs_a_test_case()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => AddInternalProblem(contest, "B", DuringRegistration));
        Assert.Single(contest.Problems);
    }

    [Fact]
    public void Internal_problem_with_test_case_can_be_added_to_published_contest_before_start()
    {
        var contest = PublishedContest();

        var problem = AddJudgeableProblem(contest, "B", StartsAt.AddTicks(-1));

        Assert.Single(problem.TestCases);
        Assert.Equal(2, contest.Problems.Count);
    }

    [Fact]
    public void Toph_problem_can_be_added_to_published_contest_without_test_cases()
    {
        var contest = PublishedContest();

        contest.AddTophProblem("B", "Add Them Up", "add-them-up", TophUrl, DuringRegistration);

        Assert.Equal(2, contest.Problems.Count);
    }

    [Fact]
    public void Draft_internal_problem_can_lose_its_last_test_case()
    {
        var contest = DraftContest();
        var problem = AddJudgeableProblem(contest, "A");

        contest.RemoveTestCase(problem.Id, 1, Now);

        Assert.Empty(problem.TestCases);
    }

    [Fact]
    public void Published_internal_problem_must_keep_its_last_test_case()
    {
        var contest = PublishedContest();
        var problem = contest.Problems.Single();

        var error = Assert.Throws<DomainException>(() => contest.RemoveTestCase(problem.Id, 1, DuringRegistration));

        Assert.Contains("must keep at least one", error.Message);
        Assert.Single(problem.TestCases);
    }

    [Fact]
    public void Published_internal_problem_can_remove_a_test_case_while_others_remain()
    {
        var contest = PublishedContest();
        var problem = contest.Problems.Single();
        contest.AddTestCase(problem.Id, "2 2", "4", isSample: false, DuringRegistration);

        contest.RemoveTestCase(problem.Id, 1, DuringRegistration);

        Assert.Equal(2, Assert.Single(problem.TestCases).Position);
    }

    [Fact]
    public void Removing_a_missing_test_case_reports_not_found()
    {
        var contest = PublishedContest();

        var error = Assert.Throws<DomainException>(() => contest.RemoveTestCase(contest.Problems.Single().Id, 99, DuringRegistration));

        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public void Test_cases_cannot_be_removed_after_start()
    {
        var contest = PublishedContest();
        var problem = contest.Problems.Single();
        contest.AddTestCase(problem.Id, "2 2", "4", isSample: false, DuringRegistration);

        Assert.Throws<DomainException>(() => contest.RemoveTestCase(problem.Id, 1, StartsAt));
    }

    [Fact]
    public void Published_contest_must_keep_one_problem()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => contest.RemoveProblem(contest.Problems.Single().Id, DuringRegistration));
    }

    [Fact]
    public void Draft_contest_can_remove_its_last_problem()
    {
        var contest = DraftContest();
        var problem = AddInternalProblem(contest, "A");

        contest.RemoveProblem(problem.Id, Now);

        Assert.Empty(contest.Problems);
    }
}
