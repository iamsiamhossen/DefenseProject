using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYAUCPChapter.API.Contests;

[ApiController]
[Route("api/contests")]
[AllowAnonymous]
public sealed class ContestsController(ContestQueries queries, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ContestSummaryResponse>> List(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var contests = await queries.ListAsync(includeDrafts: false, cancellationToken);
        return [.. contests.Select(c => ContestSummaryResponse.From(c, now))];
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContestDetailsResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var contest = await queries.FindAsync(id, includeDrafts: false, cancellationToken);
        return contest is null
            ? NotFound()
            : ContestDetailsResponse.From(contest, timeProvider.GetUtcNow().UtcDateTime);
    }
}
