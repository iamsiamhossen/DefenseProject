using System.ComponentModel.DataAnnotations;
using KYAUCPChapter.Core.Common;

namespace KYAUCPChapter.API.Auth;

public sealed class RegisterStudentRequest
{
    [Required]
    [MaxLength(FieldLengths.FullName)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [MaxLength(FieldLengths.StudentId)]
    public string StudentId { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(FieldLengths.Email)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(FieldLengths.Email)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class OrganizerAccountRequest
{
    [Required]
    [MaxLength(FieldLengths.FullName)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(FieldLengths.Email)]
    public string Email { get; init; } = string.Empty;
}

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, string? StudentId, IReadOnlyList<string> Roles);

public sealed record AntiforgeryTokenResponse(string RequestToken, string HeaderName);
