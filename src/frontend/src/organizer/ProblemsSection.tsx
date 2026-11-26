import { Link } from "react-router";
import { useApiData } from "../api/useApiData";
import { ButtonLink } from "../components/Button";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { ProblemBalloon } from "../contests/ProblemBalloon";
import { formatLimits, problemsApi, type ProblemSummary } from "../problems/problemsApi";

export function ProblemsSection({ contestId, editable }: { contestId: string; editable: boolean }) {
  const { state, reload } = useApiData(() => problemsApi.list(contestId), [contestId]);

  return (
    <section aria-labelledby="problems-heading" className="mt-10 max-w-3xl">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="problems-heading" className="text-xl font-bold">
          Problems
        </h2>
        {editable && (
          <div className="flex flex-wrap gap-3">
            <ButtonLink to={`/organizer/contests/${contestId}/problems/new/internal`} variant="secondary">
              Add internal problem
            </ButtonLink>
            <ButtonLink to={`/organizer/contests/${contestId}/problems/new/toph`} variant="secondary">
              Add Toph problem
            </ButtonLink>
          </div>
        )}
      </div>
      <div className="mt-4">
        {state.status === "loading" && <LoadingState label="Loading problems" />}
        {state.status === "error" && <LoadFailure error={state.error} onRetry={reload} />}
        {state.status === "ready" && <ProblemTable contestId={contestId} problems={state.data} />}
      </div>
    </section>
  );
}

function ProblemTable({ contestId, problems }: { contestId: string; problems: ProblemSummary[] }) {
  if (problems.length === 0) {
    return <p className="text-muted">No problems yet. A contest needs at least one problem before it can be published.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-md border border-rule bg-surface">
      <table className="w-full min-w-[36rem] text-left tabular-nums">
        <caption className="sr-only">Contest problems</caption>
        <thead>
          <tr className="border-b border-rule text-sm text-muted">
            <th scope="col" className="px-4 py-2 font-semibold">Problem</th>
            <th scope="col" className="px-4 py-2 font-semibold">Type</th>
            <th scope="col" className="px-4 py-2 font-semibold">Limits</th>
            <th scope="col" className="px-4 py-2 font-semibold">Test cases</th>
          </tr>
        </thead>
        <tbody>
          {problems.map((problem) => (
            <tr key={problem.id} className="border-b border-rule last:border-b-0">
              <td className="px-4 py-3">
                <span className="flex items-center gap-3">
                  <ProblemBalloon label={problem.label} />
                  <Link to={`/organizer/contests/${contestId}/problems/${problem.id}`} className="font-semibold text-action hover:text-action-strong">
                    {problem.title}
                  </Link>
                </span>
              </td>
              <td className="px-4 py-3">{problem.type === "Internal" ? "Internal" : "Toph reference"}</td>
              <td className="px-4 py-3">{problem.type === "Internal" ? formatLimits(problem) : "–"}</td>
              <td className="px-4 py-3">
                {problem.type === "Internal" ? (
                  <span className={problem.hiddenCount === 0 ? "text-danger" : undefined}>
                    {problem.sampleCount} sample, {problem.hiddenCount} hidden
                  </span>
                ) : (
                  "–"
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
