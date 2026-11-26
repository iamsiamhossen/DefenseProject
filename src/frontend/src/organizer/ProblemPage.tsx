import { useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { ApiError } from "../api/client";
import { useApiData } from "../api/useApiData";
import { Alert } from "../components/Alert";
import { Button, ButtonLink } from "../components/Button";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { ProblemBalloon } from "../contests/ProblemBalloon";
import { organizerContestsApi } from "../contests/contestsApi";
import { formatLimits, problemsApi, type ProblemDetail } from "../problems/problemsApi";
import { TestCasesSection } from "./TestCasesSection";

export function ProblemPage() {
  const { contestId = "", problemId = "" } = useParams();
  const saved = (useLocation().state as { saved?: string } | null)?.saved;
  const { state, reload } = useApiData(
    () => Promise.all([organizerContestsApi.get(contestId), problemsApi.get(contestId, problemId)]),
    [contestId, problemId],
  );

  if (state.status === "loading") {
    return <LoadingState label="Loading problem" />;
  }

  if (state.status === "error") {
    return <LoadFailure error={state.error} onRetry={reload} notFoundTitle="Problem not found" />;
  }

  const [{ contest }, problem] = state.data;
  const editable = contest.phase === "Draft" || contest.phase === "Published";

  return (
    <section className="max-w-4xl">
      <Link to={`/organizer/contests/${contest.id}`} className="text-action hover:text-action-strong">
        {contest.title}
      </Link>
      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <h1 className="flex items-center gap-3 text-3xl font-bold">
          <ProblemBalloon label={problem.label} />
          {problem.title}
        </h1>
        {editable && (
          <ButtonLink to={`/organizer/contests/${contest.id}/problems/${problem.id}/edit`} variant="secondary">
            Edit problem
          </ButtonLink>
        )}
      </div>
      {saved && (
        <div className="mt-4">
          <Alert tone="success">{saved}</Alert>
        </div>
      )}
      {!editable && <p className="mt-3 text-muted">This contest has started, so its problems are locked.</p>}

      {problem.type === "Internal" ? <InternalDetails problem={problem} /> : <TophDetails problem={problem} />}

      {problem.type === "Internal" && (
        <TestCasesSection contestId={contest.id} problem={problem} editable={editable} onChanged={reload} />
      )}

      {editable && <DeleteProblem contestId={contest.id} problemId={problem.id} />}
    </section>
  );
}

function InternalDetails({ problem }: { problem: ProblemDetail }) {
  return (
    <>
      <dl className="mt-6 grid max-w-xl gap-1 rounded-md border border-rule bg-surface px-4 py-3 sm:grid-cols-[10rem_1fr]">
        <dt className="text-muted">Type</dt>
        <dd className="font-semibold">Internal</dd>
        <dt className="text-muted">Limits</dt>
        <dd className="font-semibold tabular-nums">{formatLimits(problem)}</dd>
      </dl>
      <h2 className="mt-8 text-xl font-bold">Statement</h2>
      <div className="mt-3 max-w-prose rounded-md border border-rule bg-surface px-5 py-4 whitespace-pre-wrap">{problem.statement}</div>
    </>
  );
}

function TophDetails({ problem }: { problem: ProblemDetail }) {
  return (
    <>
      <dl className="mt-6 grid max-w-2xl gap-1 rounded-md border border-rule bg-surface px-4 py-3 sm:grid-cols-[10rem_1fr]">
        <dt className="text-muted">Type</dt>
        <dd className="font-semibold">Toph reference</dd>
        <dt className="text-muted">Toph problem ID</dt>
        <dd className="font-semibold break-all">{problem.externalProblemId}</dd>
        <dt className="text-muted">Toph problem URL</dt>
        <dd className="font-semibold break-all">{problem.externalProblemUrl}</dd>
      </dl>
      <p className="mt-4 max-w-prose text-muted">
        Participants can't see a statement for this problem yet. KYAU CP Chapter will only show Toph statements once an
        authorized integration is available; until then the participant view stays unavailable.
      </p>
    </>
  );
}

function DeleteProblem({ contestId, problemId }: { contestId: string; problemId: string }) {
  const navigate = useNavigate();
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function remove() {
    setDeleting(true);
    setError(null);
    try {
      await problemsApi.remove(contestId, problemId);
      navigate(`/organizer/contests/${contestId}`, { state: { saved: "Problem deleted." } });
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Deleting failed. Try again.");
      setDeleting(false);
      setConfirming(false);
    }
  }

  return (
    <section aria-labelledby="delete-problem-heading" className="mt-12 max-w-2xl border-t border-rule pt-6">
      <h2 id="delete-problem-heading" className="text-xl font-bold">
        Delete problem
      </h2>
      <p className="mt-2 text-muted">Deleting removes the problem and all of its test cases.</p>
      {error && (
        <div className="mt-4">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      {confirming ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <p className="w-full font-semibold">Delete this problem and its test cases?</p>
          <Button busy={deleting} onClick={remove} variant="danger">
            {deleting ? "Deleting…" : "Delete problem"}
          </Button>
          <Button variant="secondary" disabled={deleting} onClick={() => setConfirming(false)}>
            Keep problem
          </Button>
        </div>
      ) : (
        <Button variant="secondary" className="mt-4" onClick={() => setConfirming(true)}>
          Delete…
        </Button>
      )}
    </section>
  );
}
