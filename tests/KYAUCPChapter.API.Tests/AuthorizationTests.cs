using System.Net;

namespace KYAUCPChapter.API.Tests;

public sealed class AuthorizationTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Theory]
    [InlineData("/api/test-probe/authenticated")]
    [InlineData("/api/test-probe/student")]
    [InlineData("/api/test-probe/organizer")]
    public async Task Anonymous_requests_are_rejected_by_default(string url)
    {
        using var client = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Student_policy_admits_students_only()
    {
        using var student = await SignedInStudentAsync();
        using var organizer = await SignedInOrganizerAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await student.GetAsync("/api/test-probe/student")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await organizer.GetAsync("/api/test-probe/student")).StatusCode);
    }

    [Fact]
    public async Task Organizer_policy_admits_organizers_only()
    {
        using var student = await SignedInStudentAsync();
        using var organizer = await SignedInOrganizerAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await organizer.GetAsync("/api/test-probe/organizer")).StatusCode);
        var forbidden = await student.GetAsync("/api/test-probe/organizer");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Any_signed_in_user_passes_the_default_policy()
    {
        using var student = await SignedInStudentAsync();
        using var organizer = await SignedInOrganizerAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await student.GetAsync("/api/test-probe/authenticated")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await organizer.GetAsync("/api/test-probe/authenticated")).StatusCode);
    }
}
