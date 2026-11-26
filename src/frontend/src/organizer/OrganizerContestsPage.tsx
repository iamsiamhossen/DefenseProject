import { useApiData } from "../api/useApiData";
import { ButtonLink } from "../components/Button";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { ContestTable } from "../contests/ContestTable";
import { organizerContestsApi } from "../contests/contestsApi";

export function OrganizerContestsPage() {
  const { state, reload } = useApiData(() => organizerContestsApi.list(), []);

  return (
    <section>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-3xl font-bold">Manage contests</h1>
        <ButtonLink to="/organizer/contests/new">Create contest</ButtonLink>
      </div>
      <div className="mt-6">
        {state.status === "loading" && <LoadingState label="Loading contests" />}
        {state.status === "error" && <LoadFailure error={state.error} onRetry={reload} />}
        {state.status === "ready" &&
          (state.data.length === 0 ? (
            <p className="text-muted">No contests yet. Create one to start setting up the schedule.</p>
          ) : (
            <ContestTable
              caption="All contests"
              contests={state.data}
              linkTo={(contest) => `/organizer/contests/${contest.id}`}
              showPhase
            />
          ))}
      </div>
    </section>
  );
}
