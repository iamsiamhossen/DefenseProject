using System.Net;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KYAUCPChapter.API.Tests;

public sealed class StudentRegistrationTests(ApiFixture fixture) : ApiTest(fixture)
{
    [Fact]
    public async Task Registration_creates_a_student_account()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/auth/register", Registration("  Anika@Students.KYAU.test ", " 221-15-001 "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Anika@Students.KYAU.test", body.GetProperty("email").GetString());
        Assert.Equal("221-15-001", body.GetProperty("studentId").GetString());
        Assert.Equal([Roles.Student], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("anika@students.kyau.test");
        Assert.NotNull(user);
        Assert.Equal(("Anika Rahman", "221-15-001"), (user.FullName, user.NormalizedStudentId));
        Assert.NotEqual(ValidPassword, user.PasswordHash);
    }

    [Fact]
    public async Task Registration_does_not_sign_the_student_in()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001");

        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Registration_cannot_request_another_role()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/auth/register", new
        {
            fullName = "Anika Rahman",
            studentId = "221-15-001",
            email = "student@kyau.test",
            password = ValidPassword,
            roles = new[] { Roles.Organizer },
            role = Roles.Organizer
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([Roles.Student], (await ReadJsonAsync(response)).GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Email_must_be_unique_ignoring_case()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001");

        var response = await RegisterAsync(client, "STUDENT@kyau.test", "221-15-002");

        Assert.Equal(["Email"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Student_id_must_be_unique_ignoring_case_and_surrounding_spaces()
    {
        using var client = NewClient();
        await RegisterAsync(client, "first@kyau.test", "cse-221-001");

        var response = await RegisterAsync(client, "second@kyau.test", "  CSE-221-001 ");

        Assert.Equal(["StudentId"], await ErrorKeysAsync(response));
    }

    [Fact]
    public async Task Required_fields_are_validated()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/auth/register", new { fullName = "  " });

        Assert.Equal(["Email", "FullName", "Password", "StudentId"], (await ErrorKeysAsync(response)).Order());
    }

    [Fact]
    public async Task Recorded_field_lengths_are_enforced()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/auth/register", Registration(
            email: new string('e', 250) + "@kyau.test",
            studentId: new string('1', 51),
            fullName: new string('n', 201)));

        Assert.Equal(["Email", "FullName", "StudentId"], (await ErrorKeysAsync(response)).Order());
    }

    [Fact]
    public async Task Values_at_the_recorded_limits_are_accepted()
    {
        using var client = NewClient();

        var response = await PostAsync(client, "/api/auth/register", Registration(
            email: new string('e', 246) + "@kyau.test",
            studentId: new string('1', 50),
            fullName: new string('n', 200)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        using var client = NewClient();

        var response = await RegisterAsync(client, "not-an-email", "221-15-001");

        Assert.Equal(["Email"], await ErrorKeysAsync(response));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(129)]
    public async Task Password_length_outside_15_to_128_is_rejected(int length)
    {
        using var client = NewClient();

        var response = await RegisterAsync(client, "student@kyau.test", "221-15-001", new string('p', length));

        Assert.Equal(["Password"], await ErrorKeysAsync(response));
    }

    [Theory]
    [InlineData("correct horse b")]
    [InlineData("aaaaaaaaaaaaaaa")]
    [InlineData("contest practice every friday at the lab")]
    public async Task Password_needs_no_character_classes(string password)
    {
        using var client = NewClient();

        var response = await RegisterAsync(client, "student@kyau.test", "221-15-001", password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "student@kyau.test", password)).StatusCode);
    }

    [Fact]
    public async Task Password_at_the_maximum_length_is_accepted()
    {
        using var client = NewClient();

        var response = await RegisterAsync(client, "student@kyau.test", "221-15-001", new string('p', 128));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Too_long_password_explains_the_limit()
    {
        using var client = NewClient();

        var response = await RegisterAsync(client, "student@kyau.test", "221-15-001", new string('p', 129));

        var messages = (await ReadJsonAsync(response)).GetProperty("errors").GetProperty("Password").EnumerateArray().Select(m => m.GetString());
        Assert.Equal(["Passwords must be at most 128 characters."], messages);
    }

    [Fact]
    public async Task Failed_registration_leaves_no_account_behind()
    {
        using var client = NewClient();
        await RegisterAsync(client, "student@kyau.test", "221-15-001", "weak");

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("student@kyau.test"));
    }
}
