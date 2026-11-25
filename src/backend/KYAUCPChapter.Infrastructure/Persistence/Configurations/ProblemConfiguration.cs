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
        builder.Property(p => p.Label).HasMaxLength(10).ForMySQLHasCollation(Collations.CaseInsensitiveAccentSensitive);
        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.Statement).HasColumnType("longtext");
        builder.Property(p => p.ExternalProblemId).HasMaxLength(100);
        builder.Property(p => p.ExternalProblemUrl).HasMaxLength(500);
        builder.HasIndex(p => new { p.ContestId, p.Label }).IsUnique();

        builder.HasMany(p => p.TestCases)
            .WithOne()
            .HasForeignKey(t => t.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.TestCases).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
