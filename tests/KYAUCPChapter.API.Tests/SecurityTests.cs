using System.Net;
using System.Net.Http.Json;
using KYAUCPChapter.API.Security;

namespace KYAUCPChapter.API.Tests;

public sealed class SecurityTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    public async Task Anonymous_state_changing_requests_without_an_antiforgery_token_are_rejected(string url)
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync(url, Registration("student@kyau.test", "221-15-001"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Signed_in_state_changing_request_without_an_antiforgery_token_is_rejected()
    {
        using var client = await SignedInStudentAsync();

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });

        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Forged_antiforgery_token_is_rejected()
    {
        using var client = NewClient();
        await GetAntiforgeryTokenAsync(client);

        var response = await PostAsync(client, "/api/auth/register", Registration("student@kyau.test", "221-15-001"), antiforgeryToken: "forged-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Antiforgery_token_from_another_client_is_rejected()
    {
        using var attacker = NewClient();
        using var victim = NewClient();
        var attackerToken = await GetAntiforgeryTokenAsync(attacker);
        await GetAntiforgeryTokenAsync(victim);

        var response = await PostAsync(victim, "/api/auth/register", Registration("student@kyau.test", "221-15-001"), attackerToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_antiforgery_token_is_not_valid_after_sign_in()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001");
        var anonymousToken = await GetAntiforgeryTokenAsync(client);
        await LoginAsync(client, "student@kyau.test", ValidPassword);

        var staleToken = await PostAsync(client, "/api/auth/logout", antiforgeryToken: anonymousToken);
        var freshToken = await PostAsync(client, "/api/auth/logout");

        Assert.Equal(HttpStatusCode.BadRequest, staleToken.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, freshToken.StatusCode);
    }

    [Fact]
    public async Task Antiforgery_cookie_is_hardened()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/auth/antiforgery");

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SecurityServiceCollectionExtensions.AntiforgeryCookieName + "=", StringComparison.Ordinal)).ToLowerInvariant();
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
    }

    [Fact]
    public async Task Cors_preflight_is_allowed_for_the_configured_origin_with_credentials()
    {
        using var client = NewClient();

        var response = await client.SendAsync(Preflight(ApiFixture.AllowedOrigin));

        Assert.Equal(ApiFixture.AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task Cors_preflight_is_refused_for_other_origins()
    {
        using var client = NewClient();

        var response = await client.SendAsync(Preflight("https://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Domain_rule_violations_become_400_problem_details()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/test-probe/domain-error");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Registration is not open for this contest.", body.GetProperty("detail").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Unexpected_errors_return_500_without_internal_details()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/api/test-probe/unhandled-error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ProbeController.InternalDetail, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
    }

    [Fact]
    public async Task Health_endpoint_is_public_and_checks_the_database()
    {
        using var client = NewClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", $"content-type,{SecurityServiceCollectionExtensions.AntiforgeryHeaderName.ToLowerInvariant()}");
        return request;
    }
}
