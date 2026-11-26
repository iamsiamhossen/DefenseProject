using System.Net;
using System.Text.Json;
using KYAUCPChapter.API.Problems;
using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace KYAUCPChapter.API.Tests;

public sealed class ProblemManagementTests(ApiFixture fixture) : ApiTest(fixture)
{
    private static readonly DateTime Now = DateTime.UtcNow.AddTicks(-(DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute));

    [Fact]
    public async Task Organizer_adds_an_internal_problem_with_sample_and_hidden_test_cases()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);

        var response = await PostAsync(organizer, Problems(contestId) + "/internal", Internal(testCases:
        [
            new { input = "1 2", expectedOutput = "3", isSample = true },
            new { input = "10 20", expectedOutput = "30", isSample = false }
        ]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        var problemId = problem.GetProperty("id").GetGuid();
        Assert.Equal($"{Problems(contestId)}/{problemId}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(("A", "Internal", "Read two integers and print their sum.", 2000, 262144), (problem.GetProperty("label").GetString(), problem.GetProperty("type").GetString(), problem.GetProperty("statement").GetString(), problem.GetProperty("timeLimitMs").GetInt32(), problem.GetProperty("memoryLimitKb").GetInt32()));
        Assert.Equal([(1, true, "1 2"), (2, false, "10 20")], problem.GetProperty("testCases").EnumerateArray().Select(t => (t.GetProperty("position").GetInt32(), t.GetProperty("isSample").GetBoolean(), t.GetProperty("inputPreview").GetString())));

        var summary = Assert.Single((await ReadJsonAsync(await organizer.GetAsync(Problems(contestId)))).EnumerateArray());
        Assert.Equal((1, 1), (summary.GetProperty("sampleCount").GetInt32(), summary.GetProperty("hiddenCount").GetInt32()));
        Assert.Equal(1, (await ReadJsonAsync(await organizer.GetAsync($"/api/organizer/contests/{contestId}"))).GetProperty("problemCount").GetInt32());
    }

    [Fact]
    public async Task Organizer_adds_a_toph_problem_reference_without_statement_or_test_cases()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);

        var response = await PostAsync(organizer, Problems(contestId) + "/toph", Toph());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Equal(("Toph", "add-them-up", "https://toph.co/p/add-them-up"), (problem.GetProperty("type").GetString(), problem.GetProperty("externalProblemId").GetString(), problem.GetProperty("externalProblemUrl").GetString()));
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("statement").ValueKind);
        Assert.Empty(problem.GetProperty("testCases").EnumerateArray());

        var addTestCase = await PostAsync(organizer, $"{Problems(contestId)}/{problem.GetProperty("id").GetGuid()}/test-cases", new { input = "1", expectedOutput = "1", isSample = false });
        Assert.Equal("This operation is only available for Internal problems.", await DetailAsync(addTestCase));
    }

    [Fact]
    public async Task Only_organizers_can_manage_problems()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        using var student = await SignedInStudentAsync();
        using var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync(Problems(contestId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(student, Problems(contestId) + "/internal", Internal())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Problems(contestId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/api/contests/{contestId}/problems")).StatusCode);
    }

    [Fact]
    public async Task Internal_problem_fields_are_validated()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);

        var empty = await PostAsync(organizer, Problems(contestId) + "/internal", new { label = " " });
        var tooLarge = await PostAsync(organizer, Problems(contestId) + "/internal", Internal(label: new string('L', 11), statement: new string('s', 50_001), timeLimitMs: 15_001, memoryLimitKb: 16_383));

        Assert.Equal(["Label", "MemoryLimitKb", "Statement", "TimeLimitMs", "Title"], (await ErrorKeysAsync(empty)).Order());
        Assert.Equal(["Label", "MemoryLimitKb", "Statement", "TimeLimitMs"], (await ErrorKeysAsync(tooLarge)).Order());
    }

    [Fact]
    public async Task Values_at_the_approved_limits_are_accepted()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);

        var response = await PostAsync(organizer, Problems(contestId) + "/internal", Internal(label: new string('L', 10), statement: new string('s', 50_000), timeLimitMs: 15_000, memoryLimitKb: 524_288, testCases:
        [
            new { input = new string('i', 10_000), expectedOutput = new string('o', 10_000), isSample = true },
            new { input = new string('h', 1_048_576), expectedOutput = "x", isSample = false }
        ]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var testCases = (await ReadJsonAsync(response)).GetProperty("testCases").EnumerateArray().ToList();
        Assert.Equal(1_048_576, testCases[1].GetProperty("inputLength").GetInt32());
        Assert.Equal(300, testCases[1].GetProperty("inputPreview").GetString()!.Length);
    }

    [Fact]
    public async Task Test_case_sizes_follow_the_sample_and_hidden_limits()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId);

        var sample = await PostAsync(organizer, TestCases(contestId, problemId), new { input = new string('i', 10_001), expectedOutput = "", isSample = true });
        var hidden = await PostAsync(organizer, TestCases(contestId, problemId), new { input = "", expectedOutput = new string('o', 1_048_577), isSample = false });
        var missing = await PostAsync(organizer, TestCases(contestId, problemId), new { isSample = false });

        Assert.Equal(["Input"], await ErrorKeysAsync(sample));
        Assert.Equal(["ExpectedOutput"], await ErrorKeysAsync(hidden));
        Assert.Equal(["ExpectedOutput", "Input"], (await ErrorKeysAsync(missing)).Order());
    }

    [Fact]
    public async Task Hidden_test_case_accepts_both_fields_at_1_MB()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId);

        var response = await PostAsync(organizer, TestCases(contestId, problemId), new { input = new string('i', 1_048_576), expectedOutput = new string('o', 1_048_576), isSample = false });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = await ReadJsonAsync(await organizer.GetAsync(response.Headers.Location!.AbsolutePath));
        Assert.Equal((1_048_576, 1_048_576), (stored.GetProperty("input").GetString()!.Length, stored.GetProperty("expectedOutput").GetString()!.Length));
    }

    [Fact]
    public async Task Request_limit_accommodates_worst_case_json_escaping_of_both_hidden_fields()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId);
        var escaped = new string('\u0001', 1_048_576);
        var body = JsonSerializer.Serialize(new { input = escaped, expectedOutput = escaped, isSample = false });
        Assert.True(body.Length > 12_000_000);

        var response = await SendRawJsonAsync(organizer, HttpMethod.Post, TestCases(contestId, problemId), body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "api/organizer/contests/{contestId:guid}/problems/internal")]
    [InlineData("POST", "api/organizer/contests/{contestId:guid}/problems/{problemId:guid}/test-cases")]
    [InlineData("PUT", "api/organizer/contests/{contestId:guid}/problems/{problemId:guid}/test-cases/{position:int}")]
    public void Test_case_endpoints_declare_the_request_limit_for_two_hidden_fields(string method, string route)
    {
        var endpoint = Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == route && e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

        var limit = endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;

        Assert.Equal(ProblemRequestLimits.TestCaseRequestBytes, limit);
        Assert.True(limit >= 2L * 1_048_576 * 6, "Two 1 MB hidden fields must fit even when every byte is JSON-escaped.");
    }

    [Fact]
    public async Task Problem_labels_are_unique_within_a_contest_ignoring_case()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        await CreateInternalAsync(organizer, contestId);

        var duplicate = await PostAsync(organizer, Problems(contestId) + "/toph", Toph(label: "a"));

        Assert.Equal("Problem label 'a' is already used in this contest.", await DetailAsync(duplicate));
    }

    [Fact]
    public async Task Problems_are_only_reachable_through_their_own_contest()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestA = await CreateDraftAsync(organizer);
        var contestB = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestA);
        var wrong = $"{Problems(contestB)}/{problemId}";

        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync(wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJsonAsync(organizer, HttpMethod.Put, wrong + "/internal", Internal())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(organizer, wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(organizer, wrong + "/test-cases", new { input = "1", expectedOutput = "1", isSample = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync(wrong + "/test-cases/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync($"{Problems(Guid.NewGuid())}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await organizer.GetAsync($"{Problems(contestA)}/{problemId}")).StatusCode);
    }

    [Fact]
    public async Task Organizer_edits_problems_of_the_matching_type()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var internalId = await CreateInternalAsync(organizer, contestId);
        var tophId = (await ReadJsonAsync(await PostAsync(organizer, Problems(contestId) + "/toph", Toph()))).GetProperty("id").GetGuid();

        var editedInternal = await SendJsonAsync(organizer, HttpMethod.Put, $"{Problems(contestId)}/{internalId}/internal", Internal(label: "C", title: "Sum Again", timeLimitMs: 3000));
        var editedToph = await SendJsonAsync(organizer, HttpMethod.Put, $"{Problems(contestId)}/{tophId}/toph", Toph(label: "D", externalProblemId: "renamed"));
        var wrongType = await SendJsonAsync(organizer, HttpMethod.Put, $"{Problems(contestId)}/{tophId}/internal", Internal(label: "D"));
        var badUrl = await SendJsonAsync(organizer, HttpMethod.Put, $"{Problems(contestId)}/{tophId}/toph", Toph(label: "D", externalProblemUrl: "toph.co/p/x"));

        var internalBody = await ReadJsonAsync(editedInternal);
        Assert.Equal(("C", "Sum Again", 3000), (internalBody.GetProperty("label").GetString(), internalBody.GetProperty("title").GetString(), internalBody.GetProperty("timeLimitMs").GetInt32()));
        Assert.Equal("renamed", (await ReadJsonAsync(editedToph)).GetProperty("externalProblemId").GetString());
        Assert.Equal("This operation is only available for Internal problems.", await DetailAsync(wrongType));
        Assert.Equal("Toph problem URL must be an absolute HTTP or HTTPS URL.", await DetailAsync(badUrl));
    }

    [Fact]
    public async Task Organizer_reads_updates_and_deletes_test_cases()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId);
        var added = await PostAsync(organizer, TestCases(contestId, problemId), new { input = "4 5", expectedOutput = "9", isSample = true });
        var position = (await ReadJsonAsync(added)).GetProperty("position").GetInt32();

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var full = await ReadJsonAsync(await organizer.GetAsync($"{TestCases(contestId, problemId)}/{position}"));
        Assert.Equal(("4 5", "9", true), (full.GetProperty("input").GetString(), full.GetProperty("expectedOutput").GetString(), full.GetProperty("isSample").GetBoolean()));

        var updated = await SendJsonAsync(organizer, HttpMethod.Put, $"{TestCases(contestId, problemId)}/{position}", new { input = "6 7", expectedOutput = "13" });
        var tooLongForSample = await SendJsonAsync(organizer, HttpMethod.Put, $"{TestCases(contestId, problemId)}/{position}", new { input = new string('x', 10_001), expectedOutput = "1" });
        var deleted = await DeleteAsync(organizer, $"{TestCases(contestId, problemId)}/{position}");
        var missing = await DeleteAsync(organizer, $"{TestCases(contestId, problemId)}/{position}");

        Assert.Equal("13", (await ReadJsonAsync(updated)).GetProperty("expectedOutput").GetString());
        Assert.Equal(["Input"], await ErrorKeysAsync(tooLongForSample));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Publishing_counts_only_hidden_test_cases()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId, testCases: [new { input = "1 2", expectedOutput = "3", isSample = true }]);

        var blocked = await PostAsync(organizer, $"/api/organizer/contests/{contestId}/publish");
        await PostAsync(organizer, TestCases(contestId, problemId), new { input = "5 5", expectedOutput = "10", isSample = false });
        var published = await PostAsync(organizer, $"/api/organizer/contests/{contestId}/publish");

        Assert.Equal("Every internal problem needs at least one hidden test case before publication.", await DetailAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
    }

    [Fact]
    public async Task Published_contest_keeps_its_hidden_test_and_problem_rules()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId, testCases:
        [
            new { input = "1 2", expectedOutput = "3", isSample = false },
            new { input = "2 2", expectedOutput = "4", isSample = true }
        ]);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(organizer, $"/api/organizer/contests/{contestId}/publish")).StatusCode);

        var removeSample = await DeleteAsync(organizer, $"{TestCases(contestId, problemId)}/2");
        var removeLastHidden = await DeleteAsync(organizer, $"{TestCases(contestId, problemId)}/1");
        var removeOnlyProblem = await DeleteAsync(organizer, $"{Problems(contestId)}/{problemId}");
        var addWithoutHidden = await PostAsync(organizer, Problems(contestId) + "/internal", Internal(label: "B", testCases: [new { input = "1", expectedOutput = "1", isSample = true }]));
        var addWithHidden = await PostAsync(organizer, Problems(contestId) + "/internal", Internal(label: "B", testCases: [new { input = "1", expectedOutput = "1", isSample = false }]));

        Assert.Equal(HttpStatusCode.NoContent, removeSample.StatusCode);
        Assert.Equal("An internal problem in a published contest must keep at least one hidden test case.", await DetailAsync(removeLastHidden));
        Assert.Equal("A published contest must keep at least one problem.", await DetailAsync(removeOnlyProblem));
        Assert.Equal("An internal problem added to a published contest needs at least one hidden test case.", await DetailAsync(addWithoutHidden));
        Assert.Equal(HttpStatusCode.Created, addWithHidden.StatusCode);
    }

    [Fact]
    public async Task Problems_are_locked_once_the_contest_starts()
    {
        using var organizer = await SignedInOrganizerAsync();
        var createdAt = Now.AddDays(-2);
        var contest = Contest.Create("Started", null, ContestMode.Individual, new ContestSchedule(createdAt.AddHours(1), createdAt.AddHours(2), Now.AddMinutes(-30), 300, null), createdAt);
        var problem = contest.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3")], createdAt);
        contest.Publish(createdAt);
        await using (var db = Fixture.Database.CreateContext())
        {
            db.Contests.Add(contest);
            await db.SaveChangesAsync();
        }

        var edit = await SendJsonAsync(organizer, HttpMethod.Put, $"{Problems(contest.Id)}/{problem.Id}/internal", Internal());
        var addTest = await PostAsync(organizer, TestCases(contest.Id, problem.Id), new { input = "1", expectedOutput = "1", isSample = false });
        var add = await PostAsync(organizer, Problems(contest.Id) + "/toph", Toph(label: "B"));

        Assert.Equal("A contest cannot be edited after it starts.", await DetailAsync(edit));
        Assert.Equal("A contest cannot be edited after it starts.", await DetailAsync(addTest));
        Assert.Equal("A contest cannot be edited after it starts.", await DetailAsync(add));
    }

    [Fact]
    public async Task Draft_problem_can_be_deleted_with_its_test_cases()
    {
        using var organizer = await SignedInOrganizerAsync();
        var contestId = await CreateDraftAsync(organizer);
        var problemId = await CreateInternalAsync(organizer, contestId, testCases: [new { input = "1 2", expectedOutput = "3", isSample = false }]);

        var deleted = await DeleteAsync(organizer, $"{Problems(contestId)}/{problemId}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await ReadJsonAsync(await organizer.GetAsync(Problems(contestId)))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync($"{Problems(contestId)}/{problemId}")).StatusCode);
    }

    private static string Problems(Guid contestId) => $"/api/organizer/contests/{contestId}/problems";

    private static string TestCases(Guid contestId, Guid problemId) => $"{Problems(contestId)}/{problemId}/test-cases";

    private static object Internal(
        string label = "A",
        string title = "Sum",
        string statement = "Read two integers and print their sum.",
        int timeLimitMs = 2000,
        int memoryLimitKb = 262144,
        object[]? testCases = null) =>
        new { label, title, statement, timeLimitMs, memoryLimitKb, testCases = testCases ?? [] };

    private static object Toph(
        string label = "B",
        string title = "Add Them Up",
        string externalProblemId = "add-them-up",
        string externalProblemUrl = "https://toph.co/p/add-them-up") =>
        new { label, title, externalProblemId, externalProblemUrl };

    private static async Task<Guid> CreateDraftAsync(HttpClient organizer)
    {
        var response = await PostAsync(organizer, "/api/organizer/contests", new
        {
            title = "Problem Contest",
            mode = "Individual",
            registrationOpensAt = Now.AddDays(1),
            registrationClosesAt = Now.AddDays(2),
            startsAt = Now.AddDays(3),
            durationMinutes = 300
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("contest").GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateInternalAsync(HttpClient organizer, Guid contestId, object[]? testCases = null)
    {
        var response = await PostAsync(organizer, Problems(contestId) + "/internal", Internal(testCases: testCases));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }
}
