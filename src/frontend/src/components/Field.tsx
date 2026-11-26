import { useId, type ReactNode } from "react";

export interface FieldControlProps {
  id: string;
  "aria-invalid"?: true;
  "aria-describedby"?: string;
}

interface FieldProps {
  label: string;
  hint?: string;
  errors?: string[];
  className?: string;
  children: (control: FieldControlProps) => ReactNode;
}

export function Field({ label, hint, errors = [], className, children }: FieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const invalid = errors.length > 0;
  const describedBy = [hint ? hintId : null, invalid ? errorId : null].filter(Boolean).join(" ") || undefined;

  return (
    <div className={className}>
      <label htmlFor={id} className="block font-semibold">
        {label}
      </label>
      {hint && (
        <p id={hintId} className="mt-0.5 text-sm text-muted">
          {hint}
        </p>
      )}
      {children({ id, "aria-invalid": invalid || undefined, "aria-describedby": describedBy })}
      <FieldErrors id={errorId} errors={errors} />
    </div>
  );
}

export function FieldErrors({ id, errors }: { id: string; errors: string[] }) {
  if (errors.length === 0) {
    return null;
  }

  return (
    <ul id={id} className="mt-1.5 text-sm text-danger">
      {errors.map((error) => (
        <li key={error}>{error}</li>
      ))}
    </ul>
  );
}

export function controlClasses(invalid: boolean): string {
  return `mt-1.5 block w-full rounded-md border bg-surface px-3 py-2 text-base text-ink disabled:cursor-not-allowed disabled:bg-ground disabled:text-muted ${invalid ? "border-danger" : "border-rule"}`;
}
