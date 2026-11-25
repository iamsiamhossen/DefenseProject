using KYAUCPChapter.Core.Domain.Contests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class ContestConfiguration : IEntityTypeConfiguration<Contest>
{
    public void Configure(EntityTypeBuilder<Contest> builder)
    {
        builder.ToTable("Contests", table =>
        {
            table.HasCheckConstraint("CK_Contests_RegistrationWindow", "`RegistrationOpensAt` <= `RegistrationClosesAt` AND `RegistrationClosesAt` <= `StartsAt`");
            table.HasCheckConstraint("CK_Contests_Duration", "`DurationMinutes` > 0");
            table.HasCheckConstraint("CK_Contests_FreezeAt", "`FreezeAt` IS NULL OR (`FreezeAt` > `StartsAt` AND `FreezeAt` < DATE_ADD(`StartsAt`, INTERVAL `DurationMinutes` MINUTE))");
            table.HasCheckConstraint("CK_Contests_FinalizedAt", "(`Status` = 'Finalized') = (`FinalizedAt` IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(200);
        builder.Property(c => c.Description).HasColumnType("text");

        builder.ComplexProperty(c => c.Schedule, schedule =>
        {
            schedule.Property(s => s.RegistrationOpensAt).HasColumnName("RegistrationOpensAt");
            schedule.Property(s => s.RegistrationClosesAt).HasColumnName("RegistrationClosesAt");
            schedule.Property(s => s.StartsAt).HasColumnName("StartsAt");
            schedule.Property(s => s.DurationMinutes).HasColumnName("DurationMinutes");
            schedule.Property(s => s.FreezeAt).HasColumnName("FreezeAt");
        });

        builder.HasMany(c => c.Problems)
            .WithOne()
            .HasForeignKey(p => p.ContestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Problems).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
