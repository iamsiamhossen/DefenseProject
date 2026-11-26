using System.ComponentModel.DataAnnotations;
using System.Text;
using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Infrastructure.Persistence;

namespace KYAUCPChapter.API.Contests;

public sealed class ContestRequest : IValidatableObject
{
    public const int DescriptionMaxBytes = 65_535;

    [Required(ErrorMessage = "Enter a title.")]
    [MaxLength(FieldLengths.ContestTitle, ErrorMessage = "Use at most 200 characters.")]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Required(ErrorMessage = "Choose a participation mode.")]
    public ContestMode? Mode { get; init; }

    [Required(ErrorMessage = "Enter when registration opens.")]
    public DateTime? RegistrationOpensAt { get; init; }

    [Required(ErrorMessage = "Enter when registration closes.")]
    public DateTime? RegistrationClosesAt { get; init; }

    [Required(ErrorMessage = "Enter when the contest starts.")]
    public DateTime? StartsAt { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Duration must be at least 1 minute.")]
    public int DurationMinutes { get; init; }

    public DateTime? FreezeAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, value) in Timestamps())
        {
            if (value is { Kind: not DateTimeKind.Utc })
            {
                yield return new ValidationResult("Send this time in UTC, for example 2026-12-01T09:00:00Z.", [name]);
            }
        }

        if (Description is not null && Encoding.UTF8.GetByteCount(Description) > DescriptionMaxBytes)
        {
            yield return new ValidationResult("The description is longer than the database can store.", [nameof(Description)]);
        }

        if (StartsAt is { } start && DurationMinutes > 0 && (DateTime.MaxValue - start).TotalMinutes <= DurationMinutes)
        {
            yield return new ValidationResult("The contest would end after the latest supported date.", [nameof(DurationMinutes)]);
        }
    }

    public ContestDraft ToDraft() => new(
        Title,
        Description,
        Mode!.Value,
        new ContestSchedule(RegistrationOpensAt!.Value, RegistrationClosesAt!.Value, StartsAt!.Value, DurationMinutes, FreezeAt));

    private IEnumerable<(string Name, DateTime? Value)> Timestamps() =>
    [
        (nameof(RegistrationOpensAt), RegistrationOpensAt),
        (nameof(RegistrationClosesAt), RegistrationClosesAt),
        (nameof(StartsAt), StartsAt),
        (nameof(FreezeAt), FreezeAt)
    ];
}

public sealed record ContestSummaryResponse(
    Guid Id,
    string Title,
    ContestMode Mode,
    ContestPhase Phase,
    DateTime RegistrationOpensAt,
    DateTime RegistrationClosesAt,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsRegistrationOpen)
{
    public static ContestSummaryResponse From(Contest contest, DateTime now) => new(
        contest.Id,
        contest.Title,
        contest.Mode,
        contest.GetPhase(now),
        contest.Schedule.RegistrationOpensAt,
        contest.Schedule.RegistrationClosesAt,
        contest.Schedule.StartsAt,
        contest.Schedule.EndsAt,
        contest.IsRegistrationOpen(now));
}

public sealed record ContestDetailsResponse(
    Guid Id,
    string Title,
    string Description,
    ContestMode Mode,
    ContestPhase Phase,
    DateTime RegistrationOpensAt,
    DateTime RegistrationClosesAt,
    DateTime StartsAt,
    int DurationMinutes,
    DateTime EndsAt,
    DateTime? FreezeAt,
    bool IsRegistrationOpen)
{
    public static ContestDetailsResponse From(Contest contest, DateTime now) => new(
        contest.Id,
        contest.Title,
        contest.Description,
        contest.Mode,
        contest.GetPhase(now),
        contest.Schedule.RegistrationOpensAt,
        contest.Schedule.RegistrationClosesAt,
        contest.Schedule.StartsAt,
        contest.Schedule.DurationMinutes,
        contest.Schedule.EndsAt,
        contest.Schedule.FreezeAt,
        contest.IsRegistrationOpen(now));
}

public sealed record OrganizerContestResponse(ContestDetailsResponse Contest, int ProblemCount);
