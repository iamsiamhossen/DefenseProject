using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(FieldLengths.FullName);
        builder.Property(u => u.StudentId).HasMaxLength(FieldLengths.StudentId);
        builder.Property(u => u.NormalizedStudentId).HasMaxLength(FieldLengths.StudentId).ForMySQLHasCollation(Collations.Binary);
        builder.HasIndex(u => u.NormalizedStudentId).IsUnique();
    }
}
