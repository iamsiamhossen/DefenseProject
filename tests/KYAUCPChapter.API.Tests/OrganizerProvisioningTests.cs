using System.Net;
using KYAUCPChapter.API.Auth;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KYAUCPChapter.API.Tests;

public sealed class OrganizerProvisioningTests(ApiFixture fixture) : ApiTest(fixture)
{
    private const string Email = "organizer@kyau.test";

    [Fact]
    public async Task Command_creates_an_organizer_without_a_student_id()
    {
        var (exitCode, output) = await RunAsync(["--email", Email, "--name", "Club Organizer"], ValidPassword, ValidPassword);

        Assert.Equal(0, exitCode);
        Assert.Contains("Organizer account created", output);
        Assert.DoesNotContain(ValidPassword, output);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.Null(user.StudentId);
        Assert.Equal([Roles.Organizer], await users.GetRolesAsync(user));
    }

    [Fact]
    public async Task Provisioned_organizer_can_sign_in()
    {
        await RunAsync(["--email", Email, "--name", "Club Organizer"], ValidPassword, ValidPassword);
        using var client = NewClient();

        var response = await LoginAsync(client, Email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([Roles.Organizer], (await ReadJsonAsync(response)).GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Mismatched_passwords_create_nothing()
    {
        var (exitCode, _) = await RunAsync(["--email", Email, "--name", "Club Organizer"], ValidPassword, "Different!Passw0rd");

        Assert.Equal(2, exitCode);
        Assert.False(await ExistsAsync(Email));
    }

    [Theory]
    [InlineData("not-an-email", "Club Organizer")]
    [InlineData(Email, "")]
    public async Task Invalid_arguments_are_rejected(string email, string name)
    {
        var (exitCode, output) = await RunAsync(["--email", email, "--name", name], ValidPassword, ValidPassword);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public async Task Weak_password_is_rejected_by_the_identity_policy()
    {
        var (exitCode, _) = await RunAsync(["--email", Email, "--name", "Club Organizer"], "weak", "weak");

        Assert.Equal(1, exitCode);
        Assert.False(await ExistsAsync(Email));
    }

    [Fact]
    public async Task Existing_email_is_rejected()
    {
        await RunAsync(["--email", Email, "--name", "Club Organizer"], ValidPassword, ValidPassword);

        var (exitCode, _) = await RunAsync(["--email", Email.ToUpperInvariant(), "--name", "Another"], ValidPassword, ValidPassword);

        Assert.Equal(1, exitCode);
    }

    private async Task<(int ExitCode, string Output)> RunAsync(string[] args, params string[] secrets)
    {
        var answers = new Queue<string>(secrets);
        using var output = new StringWriter();
        var exitCode = await ProvisionOrganizerCommand.RunAsync(Fixture.Factory.Services, args, _ => answers.Count > 0 ? answers.Dequeue() : null, output);
        return (exitCode, output.ToString());
    }

    private async Task<bool> ExistsAsync(string email)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email) is not null;
    }
}
