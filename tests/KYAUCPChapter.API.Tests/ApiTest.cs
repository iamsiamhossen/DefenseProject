using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KYAUCPChapter.API.Auth;
using KYAUCPChapter.API.Security;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KYAUCPChapter.API.Tests;

[Collection(ApiCollection.Name)]
public abstract class ApiTest(ApiFixture fixture) : IAsyncLifetime
{
    protected const string ValidPassword = "Str0ng!Passw0rd";

    protected ApiFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected HttpClient NewClient() => Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    protected static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<AntiforgeryTokenResponse>("/api/auth/antiforgery");
        return response!.RequestToken;
    }

    protected static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null, string? antiforgeryToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body ?? new { })
        };
        request.Headers.Add(SecurityServiceCollectionExtensions.AntiforgeryHeaderName, antiforgeryToken ?? await GetAntiforgeryTokenAsync(client));
        return await client.SendAsync(request);
    }

    protected static object Registration(string email, string studentId, string password = ValidPassword, string fullName = "Anika Rahman") =>
        new { fullName, studentId, email, password };

    protected static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string studentId, string password = ValidPassword) =>
        await PostAsync(client, "/api/auth/register", Registration(email, studentId, password));

    protected static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = ValidPassword) =>
        await PostAsync(client, "/api/auth/login", new { email, password });

    protected async Task<HttpClient> SignedInStudentAsync(string email = "student@kyau.test", string studentId = "221-15-001")
    {
        var client = NewClient();
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(client, email, studentId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email)).StatusCode);
        return client;
    }

    protected async Task<HttpClient> SignedInOrganizerAsync(string email = "organizer@kyau.test")
    {
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var (result, _) = await scope.ServiceProvider.GetRequiredService<AccountService>()
                .CreateOrganizerAsync("Club Organizer", email, ValidPassword, CancellationToken.None);
            Assert.True(result.Succeeded);
        }

        var client = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email)).StatusCode);
        return client;
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    protected static async Task<IReadOnlyList<string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return [.. (await ReadJsonAsync(response)).GetProperty("errors").EnumerateObject().Select(p => p.Name)];
    }
}
