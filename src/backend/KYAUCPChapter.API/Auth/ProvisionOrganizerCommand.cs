using System.ComponentModel.DataAnnotations;
using KYAUCPChapter.Infrastructure.Identity;

namespace KYAUCPChapter.API.Auth;

public static class ProvisionOrganizerCommand
{
    public const string Name = "provision-organizer";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, Func<string, string?> readSecret, TextWriter output)
    {
        var request = new OrganizerAccountRequest
        {
            Email = Option(args, "--email") ?? string.Empty,
            FullName = Option(args, "--name") ?? string.Empty
        };

        var validationErrors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationErrors, validateAllProperties: true))
        {
            await output.WriteLineAsync($"Usage: {Name} --email <email> --name <full name>");
            foreach (var error in validationErrors)
            {
                await output.WriteLineAsync(error.ErrorMessage);
            }

            return 2;
        }

        var password = readSecret("Password: ");
        if (string.IsNullOrEmpty(password) || password != readSecret("Confirm password: "))
        {
            await output.WriteLineAsync("The passwords are empty or do not match.");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var (result, user) = await accounts.CreateOrganizerAsync(request.FullName, request.Email, password, CancellationToken.None);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await output.WriteLineAsync(error.Description);
            }

            return 1;
        }

        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ProvisionOrganizerCommand))
            .LogInformation("Organizer account {UserId} provisioned", user!.Id);
        await output.WriteLineAsync($"Organizer account created for {user.Email}.");
        return 0;
    }

    public static string? ReadConsoleSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var secret = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return secret.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                {
                    secret.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
            }
        }
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
