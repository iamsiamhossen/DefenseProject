using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable("Problems");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Label).HasMaxLength(FieldLengths.ProblemLabel).ForMySQLHasCollation(Collations.CaseInsensitiveAccentSensitive);
        builder.Property(p => p.Title).HasMaxLength(FieldLengths.ProblemTitle);
        builder.Property(p => p.Statement).HasColumnType("longtext");
        builder.Property(p => p.ExternalProblemId).HasMaxLength(FieldLengths.TophProblemId);
        builder.Property(p => p.ExternalProblemUrl).HasMaxLength(FieldLengths.TophProblemUrl);
        builder.HasIndex(p => new { p.ContestId, p.Label }).IsUnique();

        builder.HasMany(p => p.TestCases)
            .WithOne()
            .HasForeignKey(t => t.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.TestCases).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
