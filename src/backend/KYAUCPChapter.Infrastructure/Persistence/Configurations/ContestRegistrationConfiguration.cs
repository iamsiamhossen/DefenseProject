using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Teams;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class ContestRegistrationConfiguration : IEntityTypeConfiguration<ContestRegistration>
{
    public void Configure(EntityTypeBuilder<ContestRegistration> builder)
    {
        builder.ToTable("ContestRegistrations", table =>
            table.HasCheckConstraint("CK_ContestRegistrations_SingleParticipant", "(`StudentUserId` IS NULL) <> (`TeamId` IS NULL)"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasIndex(r => new { r.ContestId, r.StudentUserId }).IsUnique();
        builder.HasIndex(r => r.TeamId).IsUnique();

        builder.HasOne<Contest>().WithMany().HasForeignKey(r => r.ContestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(r => r.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}
