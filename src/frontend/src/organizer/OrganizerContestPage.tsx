import { useState } from "react";
import { Link, useLocation, useParams } from "react-router";
import { ApiError } from "../api/client";
import { useApiData } from "../api/useApiData";
import { Alert } from "../components/Alert";
import { Button, ButtonLink } from "../components/Button";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { ContestDetailsView } from "../contests/ContestDetailsView";
import { organizerContestsApi, type OrganizerContest } from "../contests/contestsApi";
import { ProblemsSection } from "./ProblemsSection";

export function OrganizerContestPage() {
  const { contestId = "" } = useParams();
  const saved = (useLocation().state as { saved?: string } | null)?.saved;
  const { state, reload, replace } = useApiData(() => organizerContestsApi.get(contestId), [contestId]);

  if (state.status === "loading") {
    return <LoadingState label="Loading contest" />;
  }

  if (state.status === "error") {
    return <LoadFailure error={state.error} onRetry={reload} notFoundTitle="Contest not found" />;
  }

  const { contest, problemCount } = state.data;
  const editable = contest.phase === "Draft" || contest.phase === "Published";

  return (
    <section>
      <Link to="/organizer/contests" className="text-action hover:text-action-strong">
        All contests
      </Link>
      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <h1 className="text-3xl font-bold">{contest.title}</h1>
        {editable && (
          <ButtonLink to={`/organizer/contests/${contest.id}/edit`} variant="secondary">
            Edit contest
          </ButtonLink>
        )}
      </div>
      {saved && (
        <div className="mt-4">
          <Alert tone="success">{saved}</Alert>
        </div>
      )}
      <ContestDetailsView
        contest={contest}
        extraDetails={[{ term: "Problems", value: problemCount === 1 ? "1 problem" : `${problemCount} problems` }]}
      />
      <ProblemsSection contestId={contest.id} editable={editable} />
      {contest.phase === "Draft" && <PublishPanel contestId={contest.id} onPublished={replace} />}
    </section>
  );
}

function PublishPanel({ contestId, onPublished }: { contestId: string; onPublished: (contest: OrganizerContest) => void }) {
  const [confirming, setConfirming] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function publish() {
    setPublishing(true);
    setError(null);
    try {
      onPublished(await organizerContestsApi.publish(contestId));
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Publishing failed. Try again.");
      setConfirming(false);
      setPublishing(false);
    }
  }

  return (
    <section aria-labelledby="publish-heading" className="mt-10 max-w-2xl border-t border-rule pt-6">
      <h2 id="publish-heading" className="text-xl font-bold">
        Publish
      </h2>
      <p className="mt-2 text-muted">
        Publishing makes the contest visible to everyone and opens registration on schedule. It needs at least one problem,
        and every internal problem needs a hidden test case.
      </p>
      {error && (
        <div className="mt-4">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      {confirming ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <p className="w-full font-semibold">Publish this contest now?</p>
          <Button busy={publishing} onClick={publish}>
            {publishing ? "Publishing…" : "Publish contest"}
          </Button>
          <Button variant="secondary" disabled={publishing} onClick={() => setConfirming(false)}>
            Keep as draft
          </Button>
        </div>
      ) : (
        <Button className="mt-4" onClick={() => setConfirming(true)}>
          Publish…
        </Button>
      )}
    </section>
  );
}
