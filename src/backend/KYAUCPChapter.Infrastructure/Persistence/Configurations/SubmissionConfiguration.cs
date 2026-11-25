using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Core.Domain.Registrations;
using KYAUCPChapter.Core.Domain.Submissions;
using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KYAUCPChapter.Infrastructure.Persistence.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Language).HasMaxLength(50);
        builder.Property(s => s.SourceCode).HasColumnType("mediumtext");
        builder.Property(s => s.RemoteSubmissionRef).HasMaxLength(100);
        builder.Property(s => s.FailureReason).HasMaxLength(1000);
        builder.Property(s => s.CancellationReason).HasMaxLength(500);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => new { s.RegistrationId, s.ProblemId, s.ReceivedAt });

        builder.HasOne<ContestRegistration>().WithMany().HasForeignKey(s => s.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Problem>().WithMany().HasForeignKey(s => s.ProblemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
