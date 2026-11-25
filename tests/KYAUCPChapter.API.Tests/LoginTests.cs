using System.Net;
using KYAUCPChapter.API.Security;
using KYAUCPChapter.Infrastructure.Identity;

namespace KYAUCPChapter.API.Tests;

public sealed class LoginTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Fact]
    public async Task Login_issues_a_hardened_session_cookie_and_returns_the_current_user()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001");

        var response = await LoginAsync(client, "  STUDENT@kyau.test ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("student@kyau.test", (await ReadJsonAsync(response)).GetProperty("email").GetString());

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SecurityServiceCollectionExtensions.AuthCookieName + "=", StringComparison.Ordinal));
        var attributes = cookie.ToLowerInvariant();
        Assert.Contains("secure", attributes);
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);
        Assert.DoesNotContain("domain=", attributes);
        Assert.DoesNotContain("expires=", attributes);
    }

    [Fact]
    public async Task Current_user_endpoint_returns_the_signed_in_student()
    {
        using var client = await SignedInStudentAsync();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(("student@kyau.test", "Anika Rahman", "221-15-001"), (body.GetProperty("email").GetString(), body.GetProperty("fullName").GetString(), body.GetProperty("studentId").GetString()));
        Assert.Equal([Roles.Student], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_generic_response()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001");

        var wrongPassword = await LoginAsync(client, "student@kyau.test", "Wr0ng!Password");
        var unknownEmail = await LoginAsync(client, "nobody@kyau.test");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal((await ReadJsonAsync(wrongPassword)).GetProperty("title").GetString(), (await ReadJsonAsync(unknownEmail)).GetProperty("title").GetString());
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie") && wrongPassword.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith(SecurityServiceCollectionExtensions.AuthCookieName, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(client, "student@kyau.test", "Wr0ng!Password");
        }

        var correctPassword = await LoginAsync(client, "student@kyau.test");

        Assert.Equal(HttpStatusCode.Unauthorized, correctPassword.StatusCode);
    }

    [Fact]
    public async Task Login_request_is_validated()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/auth/login", new { email = "not-an-email" });

        Assert.Equal(["Email", "Password"], (await ErrorKeysAsync(response)).Order());
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        using var client = await SignedInStudentAsync();

        var logout = await PostAsync(client, "/api/auth/logout");
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Anonymous_current_user_request_gets_401_problem_details_not_a_redirect()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
