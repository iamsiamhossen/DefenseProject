using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Scoring;
using KYAUCPChapter.Core.Domain.Submissions;
using KYAUCPChapter.Core.Domain.Teams;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KYAUCPChapter.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    private const int EnumMaxLength = 32;

    public DbSet<Contest> Contests => Set<Contest>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<ContestRegistration> ContestRegistrations => Set<ContestRegistration>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<FinalStanding> FinalStandings => Set<FinalStanding>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>().HaveColumnType("datetime(6)");
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>().HaveColumnType("datetime(6)");

        configurationBuilder.Properties<ContestMode>().HaveConversion<string>().HaveMaxLength(EnumMaxLength);
        configurationBuilder.Properties<ContestStatus>().HaveConversion<string>().HaveMaxLength(EnumMaxLength);
        configurationBuilder.Properties<ProblemType>().HaveConversion<string>().HaveMaxLength(EnumMaxLength);
        configurationBuilder.Properties<SubmissionStatus>().HaveConversion<string>().HaveMaxLength(EnumMaxLength);
        configurationBuilder.Properties<Verdict>().HaveConversion<string>().HaveMaxLength(EnumMaxLength);
    }
}
