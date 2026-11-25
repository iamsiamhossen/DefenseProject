using KYAUCPChapter.Core.Common;
using KYAUCPChapter.Core.Domain.Contests;
using static KYAUCPChapter.Core.Tests.TestData;

namespace KYAUCPChapter.Core.Tests.Contests;

public class ContestTests
{
    [Fact]
    public void New_contest_is_a_draft()
    {
        var contest = DraftContest();

        Assert.Equal(ContestStatus.Draft, contest.Status);
        Assert.Equal(ContestPhase.Draft, contest.GetPhase(DuringContest));
        Assert.Null(contest.FinalizedAt);
    }

    [Fact]
    public void Contest_title_is_required()
    {
        Assert.Throws<DomainException>(() => Contest.Create("  ", null, ContestMode.Individual, Schedule(), Now));
    }

    [Fact]
    public void Contest_cannot_be_created_with_past_timestamps()
    {
        Assert.Throws<DomainException>(() => Contest.Create("Contest", null, ContestMode.Individual, Schedule(), OpensAt.AddSeconds(1)));
    }

    [Fact]
    public void Publishing_requires_a_problem()
    {
        var contest = DraftContest();

        Assert.Throws<DomainException>(() => contest.Publish(Now));
    }

    [Fact]
    public void Only_a_draft_can_be_published()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => contest.Publish(Now));
    }

    [Fact]
    public void Draft_with_valid_problems_and_future_schedule_is_published()
    {
        var contest = DraftContest();
        AddJudgeableProblem(contest, "A");

        contest.Publish(OpensAt.AddTicks(-1));

        Assert.Equal(ContestStatus.Published, contest.Status);
    }

    [Theory]
    [MemberData(nameof(NotAllTimestampsInFuture))]
    public void Publishing_requires_all_registration_and_start_timestamps_in_future(DateTime now)
    {
        var contest = DraftContest();
        AddJudgeableProblem(contest, "A");

        Assert.Throws<DomainException>(() => contest.Publish(now));
        Assert.Equal(ContestStatus.Draft, contest.Status);
    }

    public static TheoryData<DateTime> NotAllTimestampsInFuture => new() { OpensAt, DuringRegistration, ClosesAt, StartsAt, DuringContest };

    [Fact]
    public void Publishing_requires_a_test_case_for_every_internal_problem()
    {
        var contest = DraftContest();
        AddJudgeableProblem(contest, "A");
        AddInternalProblem(contest, "B");

        Assert.Throws<DomainException>(() => contest.Publish(Now));
        Assert.Equal(ContestStatus.Draft, contest.Status);
    }

    [Fact]
    public void Toph_problems_do_not_need_test_cases_to_publish()
    {
        var contest = DraftContest();
        contest.AddTophProblem("A", "Add Them Up", "add-them-up", "https://toph.co/p/add-them-up", Now);

        contest.Publish(Now);

        Assert.Equal(ContestStatus.Published, contest.Status);
    }

    [Fact]
    public void Only_a_draft_can_be_deleted()
    {
        DraftContest().EnsureCanBeDeleted();

        Assert.Throws<DomainException>(PublishedContest().EnsureCanBeDeleted);
    }

    [Fact]
    public void Published_phase_is_derived_from_time()
    {
        var contest = PublishedContest();

        Assert.Equal(ContestPhase.Published, contest.GetPhase(StartsAt.AddTicks(-1)));
        Assert.Equal(ContestPhase.Running, contest.GetPhase(StartsAt));
        Assert.Equal(ContestPhase.Running, contest.GetPhase(EndsAt.AddTicks(-1)));
        Assert.Equal(ContestPhase.Ended, contest.GetPhase(EndsAt));
    }

    [Fact]
    public void Submissions_are_accepted_only_while_running()
    {
        var contest = PublishedContest();

        Assert.False(contest.AcceptsSubmissions(StartsAt.AddTicks(-1)));
        Assert.True(contest.AcceptsSubmissions(StartsAt));
        Assert.False(contest.AcceptsSubmissions(EndsAt));
    }

    [Fact]
    public void Problems_become_visible_to_participants_when_the_contest_starts()
    {
        var contest = PublishedContest();

        Assert.False(DraftContest().AreProblemsVisibleToParticipants(DuringContest));
        Assert.False(contest.AreProblemsVisibleToParticipants(StartsAt.AddTicks(-1)));
        Assert.True(contest.AreProblemsVisibleToParticipants(StartsAt));
        Assert.True(contest.AreProblemsVisibleToParticipants(AfterContest));
    }

    [Fact]
    public void Mode_cannot_change_after_start()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => contest.ChangeMode(ContestMode.Team, hasRegistrations: false, StartsAt));
    }

    [Fact]
    public void Freeze_time_can_be_added_before_start()
    {
        var contest = PublishedContest();

        contest.Reschedule(Schedule(FreezeAt), DuringRegistration);

        Assert.Equal(FreezeAt, contest.Schedule.FreezeAt);
    }

    [Fact]
    public void Registration_window_includes_open_time_and_excludes_close_time()
    {
        var contest = PublishedContest();

        Assert.False(contest.IsRegistrationOpen(OpensAt.AddTicks(-1)));
        Assert.True(contest.IsRegistrationOpen(OpensAt));
        Assert.True(contest.IsRegistrationOpen(ClosesAt.AddTicks(-1)));
        Assert.False(contest.IsRegistrationOpen(ClosesAt));
    }

    [Fact]
    public void Registration_is_closed_for_a_draft()
    {
        Assert.False(DraftContest().IsRegistrationOpen(DuringRegistration));
    }

    [Fact]
    public void Published_contest_can_be_edited_before_start()
    {
        var contest = PublishedContest();

        contest.UpdateDetails("Renamed", "Updated", StartsAt.AddTicks(-1));

        Assert.Equal("Renamed", contest.Title);
    }

    [Fact]
    public void Contest_cannot_be_edited_after_start()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => contest.UpdateDetails("Renamed", null, StartsAt));
        Assert.Throws<DomainException>(() => contest.Reschedule(Schedule(FreezeAt), StartsAt));
        Assert.Throws<DomainException>(() => AddJudgeableProblem(contest, "B", StartsAt));
    }

    [Fact]
    public void Mode_cannot_change_after_a_registration_exists()
    {
        var contest = PublishedContest();

        Assert.Throws<DomainException>(() => contest.ChangeMode(ContestMode.Team, hasRegistrations: true, DuringRegistration));
    }

    [Fact]
    public void Mode_can_change_without_registrations()
    {
        var contest = PublishedContest();

        contest.ChangeMode(ContestMode.Team, hasRegistrations: false, DuringRegistration);

        Assert.Equal(ContestMode.Team, contest.Mode);
    }

    [Fact]
    public void Future_timestamps_can_be_rescheduled()
    {
        var contest = DraftContest();
        var schedule = new ContestSchedule(OpensAt, ClosesAt, StartsAt.AddHours(1), DurationMinutes, null);

        contest.Reschedule(schedule, Now);

        Assert.Equal(schedule, contest.Schedule);
    }

    [Fact]
    public void Passed_timestamp_cannot_be_changed()
    {
        var contest = PublishedContest();
        var schedule = new ContestSchedule(OpensAt.AddHours(2), ClosesAt, StartsAt, DurationMinutes, null);

        Assert.Throws<DomainException>(() => contest.Reschedule(schedule, DuringRegistration));
    }

    [Fact]
    public void Future_timestamps_can_change_while_passed_ones_stay_unchanged()
    {
        var contest = PublishedContest();
        var schedule = new ContestSchedule(OpensAt, ClosesAt.AddHours(1), StartsAt.AddHours(1), DurationMinutes, null);

        contest.Reschedule(schedule, DuringRegistration);

        Assert.Equal(schedule, contest.Schedule);
    }

    [Fact]
    public void New_timestamp_cannot_be_in_the_past()
    {
        var contest = DraftContest();
        var schedule = new ContestSchedule(Now.AddHours(1), ClosesAt, StartsAt, DurationMinutes, null);

        Assert.Throws<DomainException>(() => contest.Reschedule(schedule, Now.AddHours(2)));
    }
}
