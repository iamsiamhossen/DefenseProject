import { Link, useParams } from "react-router";
import { useApiData } from "../api/useApiData";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { ContestDetailsView } from "./ContestDetailsView";
import { contestsApi } from "./contestsApi";

export function ContestDetailsPage() {
  const { contestId = "" } = useParams();
  const { state, reload } = useApiData(() => contestsApi.get(contestId), [contestId]);

  if (state.status === "loading") {
    return <LoadingState label="Loading contest" />;
  }

  if (state.status === "error") {
    return <LoadFailure error={state.error} onRetry={reload} notFoundTitle="Contest not found" />;
  }

  return (
    <section>
      <Link to="/contests" className="text-action hover:text-action-strong">
        All contests
      </Link>
      <h1 className="mt-2 text-3xl font-bold">{state.data.title}</h1>
      <ContestDetailsView contest={state.data} />
    </section>
  );
}
