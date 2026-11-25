using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Teams;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(100);
        builder.Property(t => t.NormalizedName).HasMaxLength(100).ForMySQLHasCollation(Collations.Binary);
        builder.Property(t => t.InviteCode).HasMaxLength(Team.InviteCodeLength).IsFixedLength().ForMySQLHasCollation(Collations.Binary);
        builder.HasIndex(t => new { t.ContestId, t.NormalizedName }).IsUnique();
        builder.HasIndex(t => t.InviteCode).IsUnique();

        builder.HasOne<Contest>().WithMany().HasForeignKey(t => t.ContestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.LeaderUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Members)
            .WithOne()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
