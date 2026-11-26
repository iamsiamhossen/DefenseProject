import type { InputHTMLAttributes, TextareaHTMLAttributes } from "react";
import { controlClasses, Field } from "./Field";

interface FieldTextProps {
  label: string;
  hint?: string;
  errors?: string[];
}

type TextFieldProps = FieldTextProps & Omit<InputHTMLAttributes<HTMLInputElement>, "id">;

export function TextField({ label, hint, errors = [], className, ...props }: TextFieldProps) {
  return (
    <Field label={label} hint={hint} errors={errors} className={className}>
      {(control) => <input {...control} className={controlClasses(errors.length > 0)} {...props} />}
    </Field>
  );
}

type TextAreaProps = FieldTextProps & Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "id"> & { monospace?: boolean };

export function TextArea({ label, hint, errors = [], className, monospace = false, ...props }: TextAreaProps) {
  return (
    <Field label={label} hint={hint} errors={errors} className={className}>
      {(control) => (
        <textarea
          {...control}
          className={`${controlClasses(errors.length > 0)} min-h-32 ${monospace ? "font-mono text-sm" : ""}`}
          {...props}
        />
      )}
    </Field>
  );
}
