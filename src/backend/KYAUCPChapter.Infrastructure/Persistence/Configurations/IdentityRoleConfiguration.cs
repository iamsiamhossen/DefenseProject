using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    private static readonly Guid StudentRoleId = new("0198b6a0-0000-7000-8000-000000000001");
    private static readonly Guid OrganizerRoleId = new("0198b6a0-0000-7000-8000-000000000002");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.HasData(Role(StudentRoleId, Roles.Student), Role(OrganizerRoleId, Roles.Organizer));
    }

    private static IdentityRole<Guid> Role(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString()
    };
}
