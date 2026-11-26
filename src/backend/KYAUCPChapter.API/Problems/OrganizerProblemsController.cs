using KYAUCPChapter.API.Security;
using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYAUCPChapter.API.Problems;

[ApiController]
[Route("api/organizer/contests/{contestId:guid}/problems")]
[Authorize(Policy = AuthorizationPolicies.Organizer)]
public sealed class OrganizerProblemsController(
    ProblemManagementService management,
    ProblemQueries queries,
    ContestQueries contests,
    ILogger<OrganizerProblemsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProblemSummary>>> List(Guid contestId, CancellationToken cancellationToken)
    {
        if (await contests.FindAsync(contestId, includeDrafts: true, cancellationToken) is null)
        {
            return NotFound();
        }

        return await queries.ListAsync(contestId, cancellationToken);
    }

    [HttpGet("{problemId:guid}")]
    public async Task<ActionResult<ProblemDetail>> Get(Guid contestId, Guid problemId, CancellationToken cancellationToken)
    {
        var problem = await queries.FindAsync(contestId, problemId, cancellationToken);
        return problem is null ? NotFound() : problem;
    }

    [HttpPost("internal")]
    [RequestSizeLimit(ProblemRequestLimits.TestCaseRequestBytes)]
    public async Task<ActionResult<ProblemDetail>> CreateInternal(Guid contestId, CreateInternalProblemRequest request, CancellationToken cancellationToken)
    {
        var problem = await management.AddInternalAsync(contestId, request.ToInput(), [.. request.TestCases.Select(t => t.ToContent())], cancellationToken);
        return problem is null ? NotFound() : await CreatedAsync(contestId, problem.Id, cancellationToken);
    }

    [HttpPost("toph")]
    public async Task<ActionResult<ProblemDetail>> CreateToph(Guid contestId, TophProblemRequest request, CancellationToken cancellationToken)
    {
        var problem = await management.AddTophAsync(contestId, request.ToInput(), cancellationToken);
        return problem is null ? NotFound() : await CreatedAsync(contestId, problem.Id, cancellationToken);
    }

    [HttpPut("{problemId:guid}/internal")]
    public async Task<ActionResult<ProblemDetail>> UpdateInternal(Guid contestId, Guid problemId, InternalProblemRequest request, CancellationToken cancellationToken)
    {
        var problem = await management.UpdateInternalAsync(contestId, problemId, request.ToInput(), cancellationToken);
        return problem is null ? NotFound() : await DetailAsync(contestId, problemId, "updated", cancellationToken);
    }

    [HttpPut("{problemId:guid}/toph")]
    public async Task<ActionResult<ProblemDetail>> UpdateToph(Guid contestId, Guid problemId, TophProblemRequest request, CancellationToken cancellationToken)
    {
        var problem = await management.UpdateTophAsync(contestId, problemId, request.ToInput(), cancellationToken);
        return problem is null ? NotFound() : await DetailAsync(contestId, problemId, "updated", cancellationToken);
    }

    [HttpDelete("{problemId:guid}")]
    public async Task<IActionResult> Delete(Guid contestId, Guid problemId, CancellationToken cancellationToken)
    {
        if (await management.RemoveAsync(contestId, problemId, cancellationToken) is null)
        {
            return NotFound();
        }

        logger.LogInformation("Problem {ProblemId} removed from contest {ContestId}", problemId, contestId);
        return NoContent();
    }

    [HttpPost("{problemId:guid}/test-cases")]
    [RequestSizeLimit(ProblemRequestLimits.TestCaseRequestBytes)]
    public async Task<ActionResult<TestCaseResponse>> AddTestCase(Guid contestId, Guid problemId, TestCaseRequest request, CancellationToken cancellationToken)
    {
        var testCase = await management.AddTestCaseAsync(contestId, problemId, request.ToContent(), cancellationToken);
        if (testCase is null)
        {
            return NotFound();
        }

        logger.LogInformation("Test case {Position} added to problem {ProblemId}", testCase.Position, problemId);
        return CreatedAtAction(nameof(GetTestCase), new { contestId, problemId, position = testCase.Position }, TestCaseResponse.From(testCase));
    }

    [HttpGet("{problemId:guid}/test-cases/{position:int}")]
    public async Task<ActionResult<TestCaseResponse>> GetTestCase(Guid contestId, Guid problemId, int position, CancellationToken cancellationToken)
    {
        var testCase = await queries.FindTestCaseAsync(contestId, problemId, position, cancellationToken);
        return testCase is null ? NotFound() : TestCaseResponse.From(testCase);
    }

    [HttpPut("{problemId:guid}/test-cases/{position:int}")]
    [RequestSizeLimit(ProblemRequestLimits.TestCaseRequestBytes)]
    public async Task<ActionResult<TestCaseResponse>> UpdateTestCase(Guid contestId, Guid problemId, int position, TestCaseUpdateRequest request, CancellationToken cancellationToken)
    {
        var existing = await queries.FindTestCaseAsync(contestId, problemId, position, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        foreach (var error in TestCaseLimits.Validate(existing.IsSample, request.Input!, request.ExpectedOutput!))
        {
            ModelState.AddModelError(error.MemberNames.Single(), error.ErrorMessage!);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var testCase = await management.UpdateTestCaseAsync(contestId, problemId, position, request.Input!, request.ExpectedOutput!, cancellationToken);
        return testCase is null ? NotFound() : TestCaseResponse.From(testCase);
    }

    [HttpDelete("{problemId:guid}/test-cases/{position:int}")]
    public async Task<IActionResult> DeleteTestCase(Guid contestId, Guid problemId, int position, CancellationToken cancellationToken)
    {
        if (await management.RemoveTestCaseAsync(contestId, problemId, position, cancellationToken) is null)
        {
            return NotFound();
        }

        logger.LogInformation("Test case {Position} removed from problem {ProblemId}", position, problemId);
        return NoContent();
    }

    private async Task<ActionResult<ProblemDetail>> CreatedAsync(Guid contestId, Guid problemId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Problem {ProblemId} added to contest {ContestId}", problemId, contestId);
        var detail = await queries.FindAsync(contestId, problemId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { contestId, problemId }, detail);
    }

    private async Task<ActionResult<ProblemDetail>> DetailAsync(Guid contestId, Guid problemId, string action, CancellationToken cancellationToken)
    {
        logger.LogInformation("Problem {ProblemId} {Action} in contest {ContestId}", problemId, action, contestId);
        return (await queries.FindAsync(contestId, problemId, cancellationToken))!;
    }
}
