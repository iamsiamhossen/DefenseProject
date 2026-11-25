using Microsoft.AspNetCore.Identity;

namespace KYAUCPChapter.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public string? StudentId { get; set; }

    public string? NormalizedStudentId { get; set; }
}
