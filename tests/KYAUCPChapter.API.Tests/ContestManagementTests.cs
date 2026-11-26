using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;

namespace KYAUCPChapter.API.Tests;

public sealed class ContestManagementTests(ApiFixture fixture) : ApiTest(fixture)
{
    private static readonly DateTime Now = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
    private static readonly DateTime OpensAt = Now.AddDays(1);
    private static readonly DateTime ClosesAt = Now.AddDays(2);
    private static readonly DateTime StartsAt = Now.AddDays(3);

    [Fact]
    public async Task Organizer_creates_a_draft_that_only_organizers_can_see()
    {
        using var organizer = await SignedInOrganizerAsync();

        var response = await PostAsync(organizer, "/api/organizer/contests", ContestBody(title: "  KYAU Monthly Contest 12 "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var contest = body.GetProperty("contest");
        var id = contest.GetProperty("id").GetGuid();
        Assert.Equal($"/api/organizer/contests/{id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(("KYAU Monthly Contest 12", "Draft", "Team", 0), (contest.GetProperty("title").GetString(), contest.GetProperty("phase").GetString(), contest.GetProperty("mode").GetString(), body.GetProperty("problemCount").GetInt32()));
        Assert.Equal(StartsAt, contest.GetProperty("startsAt").GetDateTime().ToUniversalTime());
        Assert.Equal(StartsAt.AddMinutes(180), contest.GetProperty("endsAt").GetDateTime().ToUniversalTime());

        Assert.Contains(id, await IdsAsync(organizer, "/api/organizer/contests"));
        using var anonymous = NewClient();
        Assert.DoesNotContain(id, await IdsAsync(anonymous, "/api/contests"));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/contests/{id}")).StatusCode);
    }

    [Fact]
    public async Task Only_organizers_can_manage_contests()
    {
        using var student = await SignedInStudentAsync();
        using var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(student, "/api/organizer/contests", ContestBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/organizer/contests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(anonymous, "/api/organizer/contests", ContestBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/organizer/contests")).StatusCode);
    }

    [Fact]
    public async Task Required_fields_are_validated()
    {
        using var organizer = await SignedInOrganizerAsync();

        var response = await PostAsync(organizer, "/api/organizer/contests", new { title = " ", startsAt = (DateTime?)null });

        Assert.Equal(["DurationMinutes", "Mode", "RegistrationClosesAt", "RegistrationOpensAt", "StartsAt", "Title"], (await ErrorKeysAsync(response)).Order());
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        Assert.Equal("Enter a title.", errors.GetProperty("Title")[0].GetString());
        Assert.Equal("Enter when the contest starts.", errors.GetProperty("StartsAt")[0].GetString());
        Assert.Equal("Duration must be at least 1 minute.", errors.GetProperty("DurationMinutes")[0].GetString());
    }

    [Fact]
    public async Task Title_uses_the_recorded_length_limit()
    {
        using var organizer = await SignedInOrganizerAsync();

        var tooLong = await PostAsync(organizer, "/api/organizer/contests", ContestBody(title: new string('t', 201)));
        var atLimit = await PostAsync(organizer, "/api/organizer/contests", ContestBody(title: new string('t', 200)));

        Assert.Equal(["Title"], await ErrorKeysAsync(tooLong));
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
    }

    [Fact]
    public async Task Times_without_a_utc_marker_are_rejected()
    {
        using var organizer = await SignedInOrganizerAsync();
        var json = JsonSerializer.Serialize(ContestBody()).Replace(StartsAt.ToString("yyyy-MM-ddTHH:mm:ssZ"), StartsAt.ToString("yyyy-MM-ddTHH:mm:ss"));

        var response = await SendRawJsonAsync(organizer, HttpMethod.Post, "/api/organizer/contests", json);

        Assert.Equal(["StartsAt"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Description_is_limited_to_the_column_capacity()
    {
        using var organizer = await SignedInOrganizerAsync();
        var fourByteCharacters = string.Concat(Enumerable.Repeat("\U0001F600", 16_384));
        Assert.Equal(65_536, Encoding.UTF8.GetByteCount(fourByteCharacters));

        var response = await PostAsync(organizer, "/api/organizer/contests", ContestBody(description: fourByteCharacters));

        Assert.Equal(["Description"], await ErrorKeysAsync(response));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Duration_must_be_positive(int minutes)
    {
        using var organizer = await SignedInOrganizerAsync();

        var response = await PostAsync(organizer, "/api/organizer/contests", ContestBody(duration: minutes));

        Assert.Equal(["DurationMinutes"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Schedule_rules_from_the_domain_are_reported()
    {
        using var organizer = await SignedInOrganizerAsync();

        var lateClose = await PostAsync(organizer, "/api/organizer/contests", ContestBody(closes: StartsAt.AddMinutes(1)));
        var pastOpen = await PostAsync(organizer, "/api/organizer/contests", ContestBody(opens: Now.AddHours(-1)));
        var freezeAtStart = await PostAsync(organizer, "/api/organizer/contests", ContestBody(freeze: StartsAt));

        Assert.Equal("Registration must close no later than the contest start.", await DetailAsync(lateClose));
        Assert.Equal("Contest timestamps cannot be set in the past.", await DetailAsync(pastOpen));
        Assert.Equal("Freeze time must be strictly between the contest start and end.", await DetailAsync(freezeAtStart));
    }

    [Fact]
    public async Task Organizer_edits_a_draft()
    {
        using var organizer = await SignedInOrganizerAsync();
        var id = await CreateAsync(organizer);

        var response = await SendRawJsonAsync(organizer, HttpMethod.Put, $"/api/organizer/contests/{id}", JsonSerializer.Serialize(ContestBody(
            title: "Renamed Contest",
            description: "Updated rules",
            mode: "Individual",
            duration: 240,
            freeze: StartsAt.AddMinutes(200))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contest = (await ReadJsonAsync(response)).GetProperty("contest");
        Assert.Equal(("Renamed Contest", "Updated rules", "Individual", 240), (contest.GetProperty("title").GetString(), contest.GetProperty("description").GetString(), contest.GetProperty("mode").GetString(), contest.GetProperty("durationMinutes").GetInt32()));
        Assert.Equal(StartsAt.AddMinutes(200), contest.GetProperty("freezeAt").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Unknown_contests_return_404()
    {
        using var organizer = await SignedInOrganizerAsync();
        var unknown = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync($"/api/organizer/contests/{unknown}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendRawJsonAsync(organizer, HttpMethod.Put, $"/api/organizer/contests/{unknown}", JsonSerializer.Serialize(ContestBody()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(organizer, $"/api/organizer/contests/{unknown}/publish")).StatusCode);
    }

    [Fact]
    public async Task Publishing_requires_a_problem()
    {
        using var organizer = await SignedInOrganizerAsync();
        var id = await CreateAsync(organizer);

        var response = await PostAsync(organizer, $"/api/organizer/contests/{id}/publish");

        Assert.Equal("A contest needs at least one problem before it can be published.", await DetailAsync(response));
        Assert.Equal("Draft", await PhaseAsync(organizer, id));
    }

    [Fact]
    public async Task Publishing_requires_hidden_test_cases_for_internal_problems()
    {
        using var organizer = await SignedInOrganizerAsync();
        var id = await CreateAsync(organizer);
        await AddProblemAsync(id, withTestCase: false);

        var response = await PostAsync(organizer, $"/api/organizer/contests/{id}/publish");

        Assert.Equal("Every internal problem needs at least one hidden test case before publication.", await DetailAsync(response));
    }

    [Fact]
    public async Task Published_contest_becomes_public_and_cannot_be_published_again()
    {
        using var organizer = await SignedInOrganizerAsync();
        var id = await CreateAsync(organizer, description: "Bring your student ID.");
        await AddProblemAsync(id, withTestCase: true);

        var published = await PostAsync(organizer, $"/api/organizer/contests/{id}/publish");
        var again = await PostAsync(organizer, $"/api/organizer/contests/{id}/publish");

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(published)).GetProperty("problemCount").GetInt32());
        Assert.Equal("Only a draft contest can be published.", await DetailAsync(again));

        using var anonymous = NewClient();
        Assert.Contains(id, await IdsAsync(anonymous, "/api/contests"));
        var details = await ReadJsonAsync(await anonymous.GetAsync($"/api/contests/{id}"));
        Assert.Equal(("Published", "Bring your student ID.", false), (details.GetProperty("phase").GetString(), details.GetProperty("description").GetString(), details.GetProperty("isRegistrationOpen").GetBoolean()));
        Assert.False(details.TryGetProperty("problemCount", out _));
    }

    [Fact]
    public async Task Contest_cannot_be_edited_after_it_starts()
    {
        using var organizer = await SignedInOrganizerAsync();
        var id = await SeedPublishedContestAsync(started: true);

        var response = await SendRawJsonAsync(organizer, HttpMethod.Put, $"/api/organizer/contests/{id}", JsonSerializer.Serialize(ContestBody()));

        Assert.Equal("A contest cannot be edited after it starts.", await DetailAsync(response));
    }

    [Fact]
    public async Task Mode_cannot_change_once_a_registration_exists()
    {
        using var organizer = await SignedInOrganizerAsync();
        using var student = await SignedInStudentAsync();
        var studentId = (await ReadJsonAsync(await student.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();
        var id = await SeedPublishedContestAsync(started: false, registeredStudent: studentId);
        var contest = (await ReadJsonAsync(await organizer.GetAsync($"/api/organizer/contests/{id}"))).GetProperty("contest");

        var modeChange = await SendRawJsonAsync(organizer, HttpMethod.Put, $"/api/organizer/contests/{id}", JsonSerializer.Serialize(Body(contest, mode: "Team")));
        var titleChange = await SendRawJsonAsync(organizer, HttpMethod.Put, $"/api/organizer/contests/{id}", JsonSerializer.Serialize(Body(contest, title: "Renamed")));

        Assert.Equal("Contest mode cannot be changed after a registration exists.", await DetailAsync(modeChange));
        Assert.Equal(HttpStatusCode.OK, titleChange.StatusCode);
    }

    [Fact]
    public async Task Public_list_shows_published_contests_newest_first_with_derived_phase()
    {
        using var organizer = await SignedInOrganizerAsync();
        var draft = await CreateAsync(organizer);
        var running = await SeedPublishedContestAsync(started: true);
        var upcoming = await SeedPublishedContestAsync(started: false);
        using var anonymous = NewClient();

        var contests = (await ReadJsonAsync(await anonymous.GetAsync("/api/contests"))).EnumerateArray().ToList();

        Assert.Equal([upcoming, running], contests.Select(c => c.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(draft, contests.Select(c => c.GetProperty("id").GetGuid()));
        Assert.Equal(["Published", "Running"], contests.Select(c => c.GetProperty("phase").GetString()));
        Assert.Equal([true, false], contests.Select(c => c.GetProperty("isRegistrationOpen").GetBoolean()));
    }

    private static object ContestBody(
        string title = "KYAU Monthly Contest",
        string? description = "Monthly practice contest.",
        string mode = "Team",
        DateTime? opens = null,
        DateTime? closes = null,
        DateTime? starts = null,
        int duration = 180,
        DateTime? freeze = null) => new
        {
            title,
            description,
            mode,
            registrationOpensAt = opens ?? OpensAt,
            registrationClosesAt = closes ?? ClosesAt,
            startsAt = starts ?? StartsAt,
            durationMinutes = duration,
            freezeAt = freeze
        };

    private static object Body(JsonElement contest, string? title = null, string? mode = null) => new
    {
        title = title ?? contest.GetProperty("title").GetString(),
        description = contest.GetProperty("description").GetString(),
        mode = mode ?? contest.GetProperty("mode").GetString(),
        registrationOpensAt = contest.GetProperty("registrationOpensAt").GetDateTime().ToUniversalTime(),
        registrationClosesAt = contest.GetProperty("registrationClosesAt").GetDateTime().ToUniversalTime(),
        startsAt = contest.GetProperty("startsAt").GetDateTime().ToUniversalTime(),
        durationMinutes = contest.GetProperty("durationMinutes").GetInt32()
    };

    private static async Task<Guid> CreateAsync(HttpClient organizer, string? description = "Monthly practice contest.")
    {
        var response = await PostAsync(organizer, "/api/organizer/contests", ContestBody(description: description));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("contest").GetProperty("id").GetGuid();
    }

    private static async Task<string?> PhaseAsync(HttpClient organizer, Guid id) =>
        (await ReadJsonAsync(await organizer.GetAsync($"/api/organizer/contests/{id}"))).GetProperty("contest").GetProperty("phase").GetString();

    private static async Task<IReadOnlyList<Guid>> IdsAsync(HttpClient client, string url) =>
        [.. (await client.GetFromJsonAsync<JsonElement>(url)).EnumerateArray().Select(c => c.GetProperty("id").GetGuid())];

    private async Task AddProblemAsync(Guid contestId, bool withTestCase)
    {
        await using var db = Fixture.Database.CreateContext();
        var contest = await db.Contests.FindAsync(contestId);
        contest!.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, withTestCase ? [new("1 2", "3")] : [], DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedPublishedContestAsync(bool started, Guid? registeredStudent = null)
    {
        var createdAt = Now.AddDays(-2);
        var schedule = started
            ? new ContestSchedule(createdAt.AddHours(1), createdAt.AddHours(2), Now.AddMinutes(-30), 300, null)
            : new ContestSchedule(Now.AddHours(-1), Now.AddDays(1), Now.AddDays(2), 300, null);
        var contest = Contest.Create("Seeded Contest", null, ContestMode.Individual, schedule, createdAt);
        contest.AddInternalProblem("A", "Sum", "Print a + b.", 1000, 262144, [new("1 2", "3")], createdAt);
        contest.Publish(createdAt);

        await using var db = Fixture.Database.CreateContext();
        db.Contests.Add(contest);
        if (registeredStudent is { } studentId)
        {
            db.ContestRegistrations.Add(ContestRegistration.ForStudent(contest, studentId, DateTime.UtcNow));
        }

        await db.SaveChangesAsync();
        return contest.Id;
    }
}
