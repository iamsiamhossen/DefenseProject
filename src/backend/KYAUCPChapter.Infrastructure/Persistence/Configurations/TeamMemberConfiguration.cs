using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Teams;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(m => new { m.TeamId, m.UserId });
        builder.HasIndex(m => new { m.ContestId, m.UserId }).IsUnique();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Contest>().WithMany().HasForeignKey(m => m.ContestId).OnDelete(DeleteBehavior.Restrict);
    }
}
