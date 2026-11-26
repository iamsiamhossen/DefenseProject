import { useApiData } from "../api/useApiData";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { ContestTable } from "./ContestTable";
import { contestsApi, type ContestSummary } from "./contestsApi";

const groups: { title: string; includes: (contest: ContestSummary) => boolean }[] = [
  { title: "Running now", includes: (contest) => contest.phase === "Running" },
  { title: "Upcoming", includes: (contest) => contest.phase === "Published" },
  { title: "Past", includes: (contest) => contest.phase === "Ended" || contest.phase === "Finalized" },
];

export function ContestListPage() {
  const { state, reload } = useApiData(() => contestsApi.list(), []);

  return (
    <section>
      <h1 className="text-3xl font-bold">Contests</h1>
      <div className="mt-6">
        {state.status === "loading" && <LoadingState label="Loading contests" />}
        {state.status === "error" && <LoadFailure error={state.error} onRetry={reload} />}
        {state.status === "ready" && <ContestGroups contests={state.data} />}
      </div>
    </section>
  );
}

function ContestGroups({ contests }: { contests: ContestSummary[] }) {
  if (contests.length === 0) {
    return <p className="text-muted">No contests have been published yet. Published contests will appear here.</p>;
  }

  return (
    <div className="space-y-10">
      {groups.map((group) => {
        const matching = contests.filter(group.includes);
        if (matching.length === 0) {
          return null;
        }

        return (
          <section key={group.title} aria-label={group.title}>
            <h2 className="mb-3 text-xl font-bold">{group.title}</h2>
            <ContestTable caption={`${group.title} contests`} contests={matching} linkTo={(contest) => `/contests/${contest.id}`} />
          </section>
        );
      })}
    </div>
  );
}
