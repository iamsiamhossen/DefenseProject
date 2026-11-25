using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class FinalStandingConfiguration : IEntityTypeConfiguration<FinalStanding>
{
    public void Configure(EntityTypeBuilder<FinalStanding> builder)
    {
        builder.ToTable("FinalStandings", table =>
        {
            table.HasCheckConstraint("CK_FinalStandings_Rank", "`Rank` >= 1");
            table.HasCheckConstraint("CK_FinalStandings_SolvedCount", "`SolvedCount` >= 0");
            table.HasCheckConstraint("CK_FinalStandings_TotalPenalty", "`TotalPenalty` >= 0");
        });

        builder.HasKey(s => s.RegistrationId);
        builder.Property(s => s.DisplayName).HasMaxLength(FieldLengths.DisplayName);
        builder.HasOne<ContestRegistration>().WithOne().HasForeignKey<FinalStanding>(s => s.RegistrationId).OnDelete(DeleteBehavior.Restrict);
    }
}
