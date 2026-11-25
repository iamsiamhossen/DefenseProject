using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.StudentId).HasMaxLength(50);
        builder.Property(u => u.NormalizedStudentId).HasMaxLength(50).ForMySQLHasCollation(Collations.Binary);
        builder.HasIndex(u => u.NormalizedStudentId).IsUnique();
    }
}
