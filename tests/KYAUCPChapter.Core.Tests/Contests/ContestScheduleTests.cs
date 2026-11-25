using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Contests;

public class ContestScheduleTests
{
    [Fact]
    public void Valid_schedule_exposes_end_time()
    {
        var schedule = Schedule(FreezeAt);

        Assert.Equal(EndsAt, schedule.EndsAt);
        Assert.Equal(FreezeAt, schedule.FreezeAt);
    }

    [Fact]
    public void Registration_may_close_exactly_at_start()
    {
        var schedule = new ContestSchedule(OpensAt, StartsAt, StartsAt, DurationMinutes, null);

        Assert.Equal(StartsAt, schedule.RegistrationClosesAt);
    }

    [Fact]
    public void Registration_cannot_close_before_it_opens()
    {
        Assert.Throws<DomainException>(() => new ContestSchedule(ClosesAt, OpensAt, StartsAt, DurationMinutes, null));
    }

    [Fact]
    public void Registration_cannot_close_after_start()
    {
        Assert.Throws<DomainException>(() => new ContestSchedule(OpensAt, StartsAt.AddMinutes(1), StartsAt, DurationMinutes, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Duration_must_be_positive(int duration)
    {
        Assert.Throws<DomainException>(() => new ContestSchedule(OpensAt, ClosesAt, StartsAt, duration, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(DurationMinutes)]
    [InlineData(DurationMinutes + 1)]
    public void Freeze_must_be_strictly_inside_contest(int minutesFromStart)
    {
        Assert.Throws<DomainException>(() => Schedule(StartsAt.AddMinutes(minutesFromStart)));
    }

    [Fact]
    public void Freeze_may_be_just_inside_contest_boundaries()
    {
        Assert.Equal(StartsAt.AddTicks(1), Schedule(StartsAt.AddTicks(1)).FreezeAt);
        Assert.Equal(EndsAt.AddTicks(-1), Schedule(EndsAt.AddTicks(-1)).FreezeAt);
    }

    [Fact]
    public void Timestamps_must_be_utc()
    {
        var localStart = DateTime.SpecifyKind(StartsAt, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => new ContestSchedule(OpensAt, ClosesAt, localStart, DurationMinutes, null));
    }
}
