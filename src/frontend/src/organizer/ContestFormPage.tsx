import { useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { ApiError, type FieldErrors } from "../api/client";
import { useApiData } from "../api/useApiData";
import { Alert } from "../components/Alert";
import { Button } from "../components/Button";
import { FieldErrors as FieldErrorList } from "../components/Field";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { StatusPage } from "../components/StatusPage";
import { TextArea, TextField } from "../components/TextField";
import { fromLocalInput, localTimeZone, modeLabels, toLocalInput } from "../contests/contestFormat";
import { organizerContestsApi, type ContestDetails, type ContestMode } from "../contests/contestsApi";
import { fieldLimits } from "../fieldLimits";

interface FormValues {
  title: string;
  description: string;
  mode: ContestMode;
  registrationOpensAt: string;
  registrationClosesAt: string;
  startsAt: string;
  durationMinutes: string;
  freezeAt: string;
}

const emptyValues: FormValues = {
  title: "",
  description: "",
  mode: "Individual",
  registrationOpensAt: "",
  registrationClosesAt: "",
  startsAt: "",
  durationMinutes: "300",
  freezeAt: "",
};

export function ContestFormPage() {
  const { contestId } = useParams();
  if (!contestId) {
    return <ContestForm />;
  }

  return <EditContestForm contestId={contestId} />;
}

function EditContestForm({ contestId }: { contestId: string }) {
  const { state, reload } = useApiData(() => organizerContestsApi.get(contestId), [contestId]);

  if (state.status === "loading") {
    return <LoadingState label="Loading contest" />;
  }

  if (state.status === "error") {
    return <LoadFailure error={state.error} onRetry={reload} notFoundTitle="Contest not found" />;
  }

  const { contest } = state.data;
  if (contest.phase !== "Draft" && contest.phase !== "Published") {
    return <StatusPage title="This contest can no longer be edited" message="A contest's settings are locked once it starts." />;
  }

  return <ContestForm existing={contest} />;
}

function toValues(contest: ContestDetails): FormValues {
  return {
    title: contest.title,
    description: contest.description,
    mode: contest.mode,
    registrationOpensAt: toLocalInput(contest.registrationOpensAt),
    registrationClosesAt: toLocalInput(contest.registrationClosesAt),
    startsAt: toLocalInput(contest.startsAt),
    durationMinutes: String(contest.durationMinutes),
    freezeAt: toLocalInput(contest.freezeAt),
  };
}

function ContestForm({ existing }: { existing?: ContestDetails }) {
  const navigate = useNavigate();
  const [values, setValues] = useState<FormValues>(existing ? toValues(existing) : emptyValues);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  function update(field: keyof FormValues) {
    return (event: { target: { value: string } }) => setValues((current) => ({ ...current, [field]: event.target.value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setFieldErrors({});
    const input = {
      title: values.title,
      description: values.description,
      mode: values.mode,
      registrationOpensAt: fromLocalInput(values.registrationOpensAt),
      registrationClosesAt: fromLocalInput(values.registrationClosesAt),
      startsAt: fromLocalInput(values.startsAt),
      durationMinutes: Number.parseInt(values.durationMinutes, 10) || 0,
      freezeAt: fromLocalInput(values.freezeAt),
    };

    try {
      const saved = existing
        ? await organizerContestsApi.update(existing.id, input)
        : await organizerContestsApi.create(input);
      navigate(`/organizer/contests/${saved.contest.id}`, {
        state: { saved: existing ? "Changes saved." : "Contest created as a draft." },
      });
    } catch (caught) {
      const apiError = caught instanceof ApiError ? caught : new ApiError(0, "Saving failed. Try again.");
      const { "": formErrors, ...byField } = apiError.fieldErrors;
      setFieldErrors(byField);
      setError(formErrors?.join(" ") ?? (Object.keys(byField).length > 0 ? null : apiError.message));
      setSubmitting(false);
    }
  }

  const cancelTo = existing ? `/organizer/contests/${existing.id}` : "/organizer/contests";
  const [loadedAt] = useState(() => Date.now());
  const passed = (iso: string | null | undefined) => Boolean(iso) && new Date(iso!).getTime() <= loadedAt;
  const lockedHint = "This time has passed and can't be changed.";

  return (
    <section className="max-w-2xl">
      <h1 className="text-3xl font-bold">{existing ? "Edit contest" : "Create contest"}</h1>
      <p className="mt-2 text-muted">
        {existing?.phase === "Published"
          ? "This contest is published. Times that have already passed can't be changed."
          : "New contests start as drafts. Only organizers can see a draft."}
      </p>
      {error && (
        <div className="mt-6">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      <form noValidate onSubmit={handleSubmit} className="mt-6 space-y-6">
        <TextField
          label="Title"
          required
          maxLength={fieldLimits.contestTitle}
          value={values.title}
          onChange={update("title")}
          errors={fieldErrors.title}
        />
        <TextArea
          label="Description"
          hint="Shown on the public contest page. Optional."
          value={values.description}
          onChange={update("description")}
          errors={fieldErrors.description}
        />
        <ModeField value={values.mode} onChange={(mode) => setValues((current) => ({ ...current, mode }))} errors={fieldErrors.mode} />

        <fieldset>
          <legend className="text-xl font-bold">Schedule</legend>
          <p className="mt-1 text-sm text-muted">
            Enter times in your time zone ({localTimeZone()}). Registration must close no later than the start.
          </p>
          <div className="mt-4 grid gap-5 sm:grid-cols-2">
            <TextField
              label="Registration opens"
              type="datetime-local"
              required
              disabled={passed(existing?.registrationOpensAt)}
              hint={passed(existing?.registrationOpensAt) ? lockedHint : undefined}
              value={values.registrationOpensAt}
              onChange={update("registrationOpensAt")}
              errors={fieldErrors.registrationOpensAt}
            />
            <TextField
              label="Registration closes"
              type="datetime-local"
              required
              disabled={passed(existing?.registrationClosesAt)}
              hint={passed(existing?.registrationClosesAt) ? lockedHint : undefined}
              value={values.registrationClosesAt}
              onChange={update("registrationClosesAt")}
              errors={fieldErrors.registrationClosesAt}
            />
            <TextField
              label="Contest starts"
              type="datetime-local"
              required
              value={values.startsAt}
              onChange={update("startsAt")}
              errors={fieldErrors.startsAt}
            />
            <TextField
              label="Duration (minutes)"
              type="number"
              inputMode="numeric"
              min={1}
              required
              value={values.durationMinutes}
              onChange={update("durationMinutes")}
              errors={fieldErrors.durationMinutes}
            />
            <TextField
              label="Freeze standings at"
              hint="Optional. Must fall strictly between the start and the end."
              type="datetime-local"
              value={values.freezeAt}
              onChange={update("freezeAt")}
              errors={fieldErrors.freezeAt}
            />
          </div>
        </fieldset>

        <div className="flex flex-wrap items-center gap-4">
          <Button type="submit" busy={submitting}>
            {submitting ? "Saving…" : existing ? "Save changes" : "Create contest"}
          </Button>
          <Link to={cancelTo} className="font-semibold text-action hover:text-action-strong">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function ModeField({ value, onChange, errors = [] }: { value: ContestMode; onChange: (mode: ContestMode) => void; errors?: string[] }) {
  const modes: ContestMode[] = ["Individual", "Team"];
  return (
    <fieldset aria-describedby={errors.length > 0 ? "mode-errors" : undefined}>
      <legend className="font-semibold">Participation</legend>
      <p className="mt-0.5 text-sm text-muted">Can't be changed once anyone has registered.</p>
      <div className="mt-2 flex flex-wrap gap-6">
        {modes.map((mode) => (
          <label key={mode} className="flex items-center gap-2">
            <input
              type="radio"
              name="mode"
              value={mode}
              checked={value === mode}
              onChange={() => onChange(mode)}
              className="size-4 accent-action"
            />
            {modeLabels[mode]}
          </label>
        ))}
      </div>
      <FieldErrorList id="mode-errors" errors={errors} />
    </fieldset>
  );
}
