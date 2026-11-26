import { useEffect, useState, type FormEvent } from "react";
import { ApiError, type FieldErrors } from "../api/client";
import { Alert } from "../components/Alert";
import { Button } from "../components/Button";
import { LoadingState } from "../components/LoadingState";
import { TextArea } from "../components/TextField";
import { fieldLimits, hiddenTestTextErrors } from "../fieldLimits";
import { problemsApi, type ProblemDetail, type TestCasePreview } from "../problems/problemsApi";

interface SectionProps {
  contestId: string;
  problem: ProblemDetail;
  editable: boolean;
  onChanged: () => void;
}

const groups = [
  { isSample: true, title: "Samples", description: "Shown to participants with the statement. Up to 10,000 characters each.", addLabel: "Add sample" },
  {
    isSample: false,
    title: "Hidden test cases",
    description: "Used for judging and never shown to participants. Up to 1 MB each.",
    addLabel: "Add hidden test case",
  },
] as const;

export function TestCasesSection({ contestId, problem, editable, onChanged }: SectionProps) {
  return (
    <div className="mt-10 space-y-10">
      {groups.map((group) => (
        <TestCaseGroup
          key={group.title}
          {...group}
          contestId={contestId}
          problem={problem}
          editable={editable}
          onChanged={onChanged}
          testCases={problem.testCases.filter((testCase) => testCase.isSample === group.isSample)}
        />
      ))}
    </div>
  );
}

interface GroupProps extends SectionProps {
  isSample: boolean;
  title: string;
  description: string;
  addLabel: string;
  testCases: TestCasePreview[];
}

function TestCaseGroup({ contestId, problem, editable, onChanged, isSample, title, description, addLabel, testCases }: GroupProps) {
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<number | null>(null);
  const headingId = `${isSample ? "samples" : "hidden"}-heading`;

  return (
    <section aria-labelledby={headingId}>
      <h2 id={headingId} className="text-xl font-bold">
        {title}
      </h2>
      <p className="mt-1 text-sm text-muted">{description}</p>
      {!isSample && problem.testCases.every((testCase) => testCase.isSample) && (
        <p className="mt-2 text-danger">Add at least one hidden test case before the contest can be published.</p>
      )}
      {testCases.length > 0 && (
        <ul className="mt-4 divide-y divide-rule rounded-md border border-rule bg-surface">
          {testCases.map((testCase, index) =>
            editing === testCase.position ? (
              <li key={testCase.position} className="px-4 py-4">
                <TestCaseEditor
                  contestId={contestId}
                  problemId={problem.id}
                  isSample={isSample}
                  position={testCase.position}
                  name={caseName(isSample, index)}
                  onDone={(changed) => {
                    setEditing(null);
                    if (changed) {
                      onChanged();
                    }
                  }}
                />
              </li>
            ) : (
              <TestCaseRow
                key={testCase.position}
                contestId={contestId}
                problemId={problem.id}
                testCase={testCase}
                name={caseName(isSample, index)}
                editable={editable}
                onEdit={() => setEditing(testCase.position)}
                onDeleted={onChanged}
              />
            ),
          )}
        </ul>
      )}
      {editable &&
        (adding ? (
          <div className="mt-4 rounded-md border border-rule bg-surface px-4 py-4">
            <TestCaseEditor
              contestId={contestId}
              problemId={problem.id}
              isSample={isSample}
              onDone={(changed) => {
                setAdding(false);
                if (changed) {
                  onChanged();
                }
              }}
            />
          </div>
        ) : (
          <Button variant="secondary" className="mt-4" onClick={() => setAdding(true)}>
            {addLabel}
          </Button>
        ))}
    </section>
  );
}

function caseName(isSample: boolean, index: number): string {
  return isSample ? `Sample ${index + 1}` : `Hidden test ${index + 1}`;
}

function TestCaseRow({
  contestId,
  problemId,
  testCase,
  name,
  editable,
  onEdit,
  onDeleted,
}: {
  contestId: string;
  problemId: string;
  testCase: TestCasePreview;
  name: string;
  editable: boolean;
  onEdit: () => void;
  onDeleted: () => void;
}) {
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function remove() {
    setDeleting(true);
    setError(null);
    try {
      await problemsApi.removeTestCase(contestId, problemId, testCase.position);
      onDeleted();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Deleting failed. Try again.");
      setDeleting(false);
      setConfirming(false);
    }
  }

  return (
    <li className="px-4 py-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="font-semibold">{name}</h3>
        {editable && (
          <div className="flex flex-wrap gap-2">
            {confirming ? (
              <>
                <Button busy={deleting} onClick={remove} variant="danger">
                  {deleting ? "Deleting…" : "Delete test case"}
                </Button>
                <Button variant="secondary" disabled={deleting} onClick={() => setConfirming(false)}>
                  Keep
                </Button>
              </>
            ) : (
              <>
                <Button variant="secondary" onClick={onEdit}>
                  Edit
                </Button>
                <Button variant="secondary" onClick={() => setConfirming(true)}>
                  Delete…
                </Button>
              </>
            )}
          </div>
        )}
      </div>
      {error && (
        <div className="mt-3">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      <div className="mt-3 grid gap-4 sm:grid-cols-2">
        <Preview label="Input" text={testCase.inputPreview} length={testCase.inputLength} />
        <Preview label="Expected output" text={testCase.outputPreview} length={testCase.outputLength} />
      </div>
    </li>
  );
}

function Preview({ label, text, length }: { label: string; text: string; length: number }) {
  return (
    <div>
      <p className="text-sm text-muted">
        {label}
        {length > text.length && <span className="tabular-nums"> (first {text.length.toLocaleString()} of {length.toLocaleString()} characters)</span>}
      </p>
      <pre className="mt-1 max-h-48 overflow-auto rounded-md border border-rule bg-ground px-3 py-2 font-mono text-sm whitespace-pre-wrap break-all">
        {text.length === 0 ? <span className="text-muted">(empty)</span> : text}
      </pre>
    </div>
  );
}

function TestCaseEditor({
  contestId,
  problemId,
  isSample,
  position,
  name,
  onDone,
}: {
  contestId: string;
  problemId: string;
  isSample: boolean;
  position?: number;
  name?: string;
  onDone: (changed: boolean) => void;
}) {
  const [values, setValues] = useState<{ input: string; expectedOutput: string } | null>(position === undefined ? { input: "", expectedOutput: "" } : null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  useEffect(() => {
    if (position === undefined) {
      return;
    }

    let active = true;
    problemsApi
      .getTestCase(contestId, problemId, position)
      .then((testCase) => active && setValues({ input: testCase.input, expectedOutput: testCase.expectedOutput }))
      .catch((caught: unknown) => active && setLoadError(caught instanceof ApiError ? caught.message : "Loading failed."));
    return () => {
      active = false;
    };
  }, [contestId, problemId, position]);

  if (loadError) {
    return <Alert tone="error">{loadError}</Alert>;
  }

  if (!values) {
    return <LoadingState label="Loading test case" />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!values) {
      return;
    }

    setError(null);
    if (!isSample) {
      const sizeErrors: FieldErrors = {};
      const inputErrors = hiddenTestTextErrors(values.input);
      const outputErrors = hiddenTestTextErrors(values.expectedOutput);
      if (inputErrors) {
        sizeErrors.input = inputErrors;
      }

      if (outputErrors) {
        sizeErrors.expectedOutput = outputErrors;
      }

      if (Object.keys(sizeErrors).length > 0) {
        setFieldErrors(sizeErrors);
        return;
      }
    }

    setSaving(true);
    setFieldErrors({});
    try {
      if (position === undefined) {
        await problemsApi.addTestCase(contestId, problemId, { ...values, isSample });
      } else {
        await problemsApi.updateTestCase(contestId, problemId, position, values);
      }

      onDone(true);
    } catch (caught) {
      const apiError = caught instanceof ApiError ? caught : new ApiError(0, "Saving failed. Try again.");
      setFieldErrors(apiError.fieldErrors);
      setError(Object.keys(apiError.fieldErrors).length > 0 ? null : apiError.message);
      setSaving(false);
    }
  }

  const kind = isSample ? "sample" : "hidden test case";
  return (
    <form noValidate onSubmit={handleSubmit} aria-label={position === undefined ? `New ${kind}` : `Edit ${name}`}>
      {error && (
        <div className="mb-4">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <TextArea
          label="Input"
          monospace
          rows={8}
          maxLength={isSample ? fieldLimits.sampleTestText : undefined}
          value={values.input}
          onChange={(event) => setValues({ ...values, input: event.target.value })}
          errors={fieldErrors.input}
        />
        <TextArea
          label="Expected output"
          monospace
          rows={8}
          maxLength={isSample ? fieldLimits.sampleTestText : undefined}
          value={values.expectedOutput}
          onChange={(event) => setValues({ ...values, expectedOutput: event.target.value })}
          errors={fieldErrors.expectedOutput}
        />
      </div>
      <div className="mt-4 flex flex-wrap gap-3">
        <Button type="submit" busy={saving}>
          {saving ? "Saving…" : position === undefined ? `Add ${kind}` : "Save test case"}
        </Button>
        <Button variant="secondary" disabled={saving} onClick={() => onDone(false)}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
