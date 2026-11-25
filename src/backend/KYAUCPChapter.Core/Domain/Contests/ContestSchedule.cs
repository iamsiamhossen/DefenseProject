using KYAUCPChapter.Core.Common;

namespace KYAUCPChapter.Core.Domain.Contests;

public sealed record ContestSchedule
{
    public ContestSchedule(
        DateTime registrationOpensAt,
        DateTime registrationClosesAt,
        DateTime startsAt,
        int durationMinutes,
        DateTime? freezeAt)
    {
        RegistrationOpensAt = Guard.Utc(registrationOpensAt, nameof(registrationOpensAt));
        RegistrationClosesAt = Guard.Utc(registrationClosesAt, nameof(registrationClosesAt));
        StartsAt = Guard.Utc(startsAt, nameof(startsAt));
        DurationMinutes = Guard.Positive(durationMinutes, "Contest duration");
        FreezeAt = freezeAt is { } freeze ? Guard.Utc(freeze, nameof(freezeAt)) : null;

        if (RegistrationOpensAt > RegistrationClosesAt)
        {
            throw new DomainException("Registration cannot close before it opens.");
        }

        if (RegistrationClosesAt > StartsAt)
        {
            throw new DomainException("Registration must close no later than the contest start.");
        }

        if (FreezeAt <= StartsAt || FreezeAt >= EndsAt)
        {
            throw new DomainException("Freeze time must be strictly between the contest start and end.");
        }
    }

    public DateTime RegistrationOpensAt { get; }

    public DateTime RegistrationClosesAt { get; }

    public DateTime StartsAt { get; }

    public int DurationMinutes { get; }

    public DateTime? FreezeAt { get; }

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);
}
