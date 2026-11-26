using Microsoft.AspNetCore.Identity;

namespace KYAUCPChapter.Infrastructure.Identity;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;
    public const string TooLongCode = "PasswordTooLong";

    public static void Apply(PasswordOptions options)
    {
        options.RequiredLength = MinimumLength;
        options.RequiredUniqueChars = 1;
        options.RequireDigit = false;
        options.RequireLowercase = false;
        options.RequireUppercase = false;
        options.RequireNonAlphanumeric = false;
    }
}

public sealed class MaximumPasswordLengthValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password) =>
        Task.FromResult(password is { Length: > PasswordPolicy.MaximumLength }
            ? IdentityResult.Failed(new IdentityError
            {
                Code = PasswordPolicy.TooLongCode,
                Description = $"Passwords must be at most {PasswordPolicy.MaximumLength} characters."
            })
            : IdentityResult.Success);
}
