import { useState, type FormEvent, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { ApiError, type FieldErrors } from "../api/client";
import { useApiData } from "../api/useApiData";
import { Alert } from "../components/Alert";
import { Button } from "../components/Button";
import { LoadFailure } from "../components/LoadFailure";
import { LoadingState } from "../components/LoadingState";
import { StatusPage } from "../components/StatusPage";
import { TextArea, TextField } from "../components/TextField";
import { organizerContestsApi, type ContestDetails } from "../contests/contestsApi";
import { fieldLimits, hiddenTestTextErrors, problemLimits } from "../fieldLimits";
import { problemsApi, type ProblemDetail, type ProblemType } from "../problems/problemsApi";

export function ProblemFormPage() {
  const { contestId = "", problemId, type } = useParams();
  const { state, reload } = useApiData(
    () => Promise.all([organizerContestsApi.get(contestId), problemId ? problemsApi.get(contestId, problemId) : Promise.resolve(null)]),
    [contestId, problemId],
  );

  if (state.status === "loading") {
    return <LoadingState label="Loading" />;
  }

  if (state.status === "error") {
    return <LoadFailure error={state.error} onRetry={reload} notFoundTitle="Not found" />;
  }

  const [{ contest }, existing] = state.data;
  if (contest.phase !== "Draft" && contest.phase !== "Published") {
    return <StatusPage title="Problems can no longer be changed" message="A contest's problems are locked once it starts." />;
  }

  const problemType: ProblemType = existing?.type ?? (type === "toph" ? "Toph" : "Internal");
  return problemType === "Internal" ? (
    <InternalProblemForm contest={contest} existing={existing} />
  ) : (
    <TophProblemForm contest={contest} existing={existing} />
  );
}

interface FormProps {
  contest: ContestDetails;
  existing: ProblemDetail | null;
}

function useProblemSubmit(contest: ContestDetails) {
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  async function submit(save: () => Promise<ProblemDetail>, message: string) {
    setSubmitting(true);
    setError(null);
    setFieldErrors({});
    try {
      const saved = await save();
      navigate(`/organizer/contests/${contest.id}/problems/${saved.id}`, { state: { saved: message } });
    } catch (caught) {
      const apiError = caught instanceof ApiError ? caught : new ApiError(0, "Saving failed. Try again.");
      const { "": formErrors, ...byField } = apiError.fieldErrors;
      setFieldErrors(byField);
      setError(formErrors?.join(" ") ?? (Object.keys(byField).length > 0 ? null : apiError.message));
      setSubmitting(false);
    }
  }

  return { submit, submitting, error, fieldErrors };
}

function InternalProblemForm({ contest, existing }: FormProps) {
  const [values, setValues] = useState({
    label: existing?.label ?? "",
    title: existing?.title ?? "",
    statement: existing?.statement ?? "",
    timeLimitMs: String(existing?.timeLimitMs ?? 1000),
    memoryMb: String((existing?.memoryLimitKb ?? 262_144) / 1024),
    hiddenInput: "",
    hiddenOutput: "",
  });
  const needsHiddenCase = !existing && contest.phase === "Published";
  const [addHiddenCase, setAddHiddenCase] = useState(needsHiddenCase);
  const { submit, submitting, error, fieldErrors } = useProblemSubmit(contest);

  const update = (field: keyof typeof values) => (event: { target: { value: string } }) =>
    setValues((current) => ({ ...current, [field]: event.target.value }));

  const [sizeErrors, setSizeErrors] = useState<FieldErrors>({});

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const inputSize = addHiddenCase ? hiddenTestTextErrors(values.hiddenInput) : undefined;
    const outputSize = addHiddenCase ? hiddenTestTextErrors(values.hiddenOutput) : undefined;
    setSizeErrors({ ...(inputSize ? { input: inputSize } : {}), ...(outputSize ? { output: outputSize } : {}) });
    if (inputSize || outputSize) {
      return;
    }

    const input = {
      label: values.label,
      title: values.title,
      statement: values.statement,
      timeLimitMs: Number.parseInt(values.timeLimitMs, 10) || 0,
      memoryLimitKb: Math.round((Number.parseFloat(values.memoryMb) || 0) * 1024),
    };
    void submit(
      () =>
        existing
          ? problemsApi.updateInternal(contest.id, existing.id, input)
          : problemsApi.createInternal(
              contest.id,
              input,
              addHiddenCase ? [{ input: values.hiddenInput, expectedOutput: values.hiddenOutput, isSample: false }] : [],
            ),
      existing ? "Problem saved." : "Problem added.",
    );
  }

  return (
    <ProblemFormLayout contest={contest} existing={existing} title={existing ? "Edit internal problem" : "Add internal problem"} error={error}>
      <form noValidate onSubmit={handleSubmit} className="mt-6 space-y-6">
        <LabelAndTitle values={values} update={update} fieldErrors={fieldErrors} />
        <TextArea
          label="Statement"
          hint="Plain text; line breaks are kept. Include input and output format and constraints."
          required
          maxLength={fieldLimits.problemStatement}
          rows={14}
          value={values.statement}
          onChange={update("statement")}
          errors={fieldErrors.statement}
        />
        <div className="grid gap-5 sm:grid-cols-2">
          <TextField
            label="Time limit (ms)"
            hint={`${problemLimits.minimumTimeLimitMs.toLocaleString()} to ${problemLimits.maximumTimeLimitMs.toLocaleString()} ms`}
            type="number"
            inputMode="numeric"
            min={problemLimits.minimumTimeLimitMs}
            max={problemLimits.maximumTimeLimitMs}
            step={100}
            required
            value={values.timeLimitMs}
            onChange={update("timeLimitMs")}
            errors={fieldErrors.timeLimitMs}
          />
          <TextField
            label="Memory limit (MB)"
            hint={`${problemLimits.minimumMemoryMb} to ${problemLimits.maximumMemoryMb} MB`}
            type="number"
            inputMode="numeric"
            min={problemLimits.minimumMemoryMb}
            max={problemLimits.maximumMemoryMb}
            required
            value={values.memoryMb}
            onChange={update("memoryMb")}
            errors={fieldErrors.memoryLimitKb}
          />
        </div>
        {!existing && (
          <fieldset>
            <legend className="text-xl font-bold">First hidden test case</legend>
            {needsHiddenCase ? (
              <p className="mt-1 text-sm text-muted">This contest is published, so a new problem must start with a hidden test case.</p>
            ) : (
              <label className="mt-2 flex items-center gap-2">
                <input type="checkbox" checked={addHiddenCase} onChange={(event) => setAddHiddenCase(event.target.checked)} className="size-4 accent-action" />
                Add one now (you can also add test cases after saving)
              </label>
            )}
            {addHiddenCase && (
              <div className="mt-4 grid gap-5 sm:grid-cols-2">
                <TextArea label="Input" monospace rows={6} value={values.hiddenInput} onChange={update("hiddenInput")} errors={sizeErrors.input ?? fieldErrors["testCases[0].Input"]} />
                <TextArea label="Expected output" monospace rows={6} value={values.hiddenOutput} onChange={update("hiddenOutput")} errors={sizeErrors.output ?? fieldErrors["testCases[0].ExpectedOutput"]} />
              </div>
            )}
          </fieldset>
        )}
        <FormActions contest={contest} existing={existing} submitting={submitting} />
      </form>
    </ProblemFormLayout>
  );
}

function TophProblemForm({ contest, existing }: FormProps) {
  const [values, setValues] = useState({
    label: existing?.label ?? "",
    title: existing?.title ?? "",
    externalProblemId: existing?.externalProblemId ?? "",
    externalProblemUrl: existing?.externalProblemUrl ?? "",
  });
  const { submit, submitting, error, fieldErrors } = useProblemSubmit(contest);

  const update = (field: keyof typeof values) => (event: { target: { value: string } }) =>
    setValues((current) => ({ ...current, [field]: event.target.value }));

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submit(
      () => (existing ? problemsApi.updateToph(contest.id, existing.id, values) : problemsApi.createToph(contest.id, values)),
      existing ? "Problem saved." : "Problem added.",
    );
  }

  return (
    <ProblemFormLayout contest={contest} existing={existing} title={existing ? "Edit Toph problem" : "Add Toph problem"} error={error}>
      <p className="mt-2 max-w-prose text-muted">
        Only organizers see this reference. Participants won't see a statement for this problem until an authorized Toph
        integration is available.
      </p>
      <form noValidate onSubmit={handleSubmit} className="mt-6 space-y-6">
        <LabelAndTitle values={values} update={update} fieldErrors={fieldErrors} />
        <div className="grid gap-5 sm:grid-cols-2">
          <TextField
            label="Toph problem ID"
            required
            maxLength={fieldLimits.tophProblemId}
            value={values.externalProblemId}
            onChange={update("externalProblemId")}
            errors={fieldErrors.externalProblemId}
          />
          <TextField
            label="Toph problem URL"
            type="url"
            required
            maxLength={fieldLimits.tophProblemUrl}
            value={values.externalProblemUrl}
            onChange={update("externalProblemUrl")}
            errors={fieldErrors.externalProblemUrl}
          />
        </div>
        <FormActions contest={contest} existing={existing} submitting={submitting} />
      </form>
    </ProblemFormLayout>
  );
}

function ProblemFormLayout({ contest, existing, title, error, children }: FormProps & { title: string; error: string | null; children: ReactNode }) {
  return (
    <section className="max-w-3xl">
      <Link to={existing ? `/organizer/contests/${contest.id}/problems/${existing.id}` : `/organizer/contests/${contest.id}`} className="text-action hover:text-action-strong">
        {existing ? existing.title : contest.title}
      </Link>
      <h1 className="mt-2 text-3xl font-bold">{title}</h1>
      {error && (
        <div className="mt-6">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      {children}
    </section>
  );
}

function LabelAndTitle<T extends { label: string; title: string }>({
  values,
  update,
  fieldErrors,
}: {
  values: T;
  update: (field: keyof T) => (event: { target: { value: string } }) => void;
  fieldErrors: FieldErrors;
}) {
  return (
    <div className="grid gap-5 sm:grid-cols-[10rem_1fr]">
      <TextField
        label="Label"
        hint="For example A"
        required
        maxLength={fieldLimits.problemLabel}
        value={values.label}
        onChange={update("label")}
        errors={fieldErrors.label}
      />
      <TextField label="Title" required maxLength={fieldLimits.problemTitle} value={values.title} onChange={update("title")} errors={fieldErrors.title} />
    </div>
  );
}

function FormActions({ contest, existing, submitting }: FormProps & { submitting: boolean }) {
  return (
    <div className="flex flex-wrap items-center gap-4">
      <Button type="submit" busy={submitting}>
        {submitting ? "Saving…" : existing ? "Save changes" : "Add problem"}
      </Button>
      <Link
        to={existing ? `/organizer/contests/${contest.id}/problems/${existing.id}` : `/organizer/contests/${contest.id}`}
        className="font-semibold text-action hover:text-action-strong"
      >
        Cancel
      </Link>
    </div>
  );
}
