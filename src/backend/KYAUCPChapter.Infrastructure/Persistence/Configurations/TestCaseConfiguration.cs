using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("TestCases");
        builder.HasKey(t => new { t.ProblemId, t.Position });
        builder.Property(t => t.Position).ValueGeneratedNever();
        builder.Property(t => t.Input).HasColumnType("longtext");
        builder.Property(t => t.ExpectedOutput).HasColumnType("longtext");
    }
}
