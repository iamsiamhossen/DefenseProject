using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace KYAUCPChapter.Infrastructure.Identity;

public sealed class AccountService(AppDbContext db, UserManager<ApplicationUser> userManager)
{
    public const string DuplicateStudentIdCode = "DuplicateStudentId";

    private const int DuplicateEntryErrorNumber = 1062;

    public async Task<(IdentityResult Result, ApplicationUser? User)> RegisterStudentAsync(
        string fullName,
        string studentId,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var trimmedStudentId = studentId.Trim();
        var normalizedStudentId = NormalizeStudentId(trimmedStudentId);
        if (await db.Users.AnyAsync(u => u.NormalizedStudentId == normalizedStudentId, cancellationToken))
        {
            return (DuplicateStudentId(), null);
        }

        var user = NewUser(fullName, email);
        user.StudentId = trimmedStudentId;
        user.NormalizedStudentId = normalizedStudentId;
        return await CreateWithRoleAsync(user, password, Roles.Student, cancellationToken);
    }

    public Task<(IdentityResult Result, ApplicationUser? User)> CreateOrganizerAsync(
        string fullName,
        string email,
        string password,
        CancellationToken cancellationToken) =>
        CreateWithRoleAsync(NewUser(fullName, email), password, Roles.Organizer, cancellationToken);

    public static string NormalizeStudentId(string studentId) => studentId.Trim().ToUpperInvariant();

    private static ApplicationUser NewUser(string fullName, string email)
    {
        var trimmedEmail = email.Trim();
        return new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            FullName = fullName.Trim(),
            Email = trimmedEmail,
            UserName = trimmedEmail
        };
    }

    private async Task<(IdentityResult Result, ApplicationUser? User)> CreateWithRoleAsync(
        ApplicationUser user,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                result = await userManager.AddToRoleAsync(user, role);
            }

            if (!result.Succeeded)
            {
                return (result, null);
            }

            await transaction.CommitAsync(cancellationToken);
            return (result, user);
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: DuplicateEntryErrorNumber })
        {
            return (IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.DuplicateEmail),
                Description = "An account with this email or student ID already exists."
            }), null);
        }
    }

    private static IdentityResult DuplicateStudentId() => IdentityResult.Failed(new IdentityError
    {
        Code = DuplicateStudentIdCode,
        Description = "An account with this student ID already exists."
    });
}
