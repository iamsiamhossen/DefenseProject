using KYAUCPChapter.API.Security;
using KYAUCPChapter.Core.Domain.Contests;
using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYAUCPChapter.API.Contests;

[ApiController]
[Route("api/organizer/contests")]
[Authorize(Policy = AuthorizationPolicies.Organizer)]
public sealed class OrganizerContestsController(
    ContestManagementService management,
    ContestQueries queries,
    TimeProvider timeProvider,
    ILogger<OrganizerContestsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ContestSummaryResponse>> List(CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var contests = await queries.ListAsync(includeDrafts: true, cancellationToken);
        return [.. contests.Select(c => ContestSummaryResponse.From(c, now))];
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizerContestResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var contest = await queries.FindAsync(id, includeDrafts: true, cancellationToken);
        return contest is null ? NotFound() : await ToResponseAsync(contest, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<OrganizerContestResponse>> Create(ContestRequest request, CancellationToken cancellationToken)
    {
        var contest = await management.CreateAsync(request.ToDraft(), cancellationToken);
        logger.LogInformation("Contest {ContestId} created", contest.Id);
        return CreatedAtAction(nameof(Get), new { id = contest.Id }, await ToResponseAsync(contest, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizerContestResponse>> Update(Guid id, ContestRequest request, CancellationToken cancellationToken)
    {
        var contest = await management.UpdateAsync(id, request.ToDraft(), cancellationToken);
        if (contest is null)
        {
            return NotFound();
        }

        logger.LogInformation("Contest {ContestId} updated", contest.Id);
        return await ToResponseAsync(contest, cancellationToken);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<OrganizerContestResponse>> Publish(Guid id, CancellationToken cancellationToken)
    {
        var contest = await management.PublishAsync(id, cancellationToken);
        if (contest is null)
        {
            return NotFound();
        }

        logger.LogInformation("Contest {ContestId} published", contest.Id);
        return await ToResponseAsync(contest, cancellationToken);
    }

    private async Task<OrganizerContestResponse> ToResponseAsync(Contest contest, CancellationToken cancellationToken) =>
        new(ContestDetailsResponse.From(contest, UtcNow()), await queries.CountProblemsAsync(contest.Id, cancellationToken));

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
