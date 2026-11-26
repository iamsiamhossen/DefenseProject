import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router";
import { ApiError, type FieldErrors } from "../api/client";
import { Alert } from "../components/Alert";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { authApi, type StudentRegistration } from "./authApi";
import { fieldLimits } from "../fieldLimits";

const emptyForm: StudentRegistration = { fullName: "", studentId: "", email: "", password: "" };

export function RegisterPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  function update(field: keyof StudentRegistration) {
    return (event: { target: { value: string } }) => setForm((current) => ({ ...current, [field]: event.target.value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setFieldErrors({});
    try {
      const account = await authApi.register(form);
      navigate("/sign-in", { state: { registeredEmail: account.email } });
    } catch (caught) {
      const apiError = caught instanceof ApiError ? caught : new ApiError(0, "Creating the account failed. Try again.");
      const { "": formErrors, ...byField } = apiError.fieldErrors;
      setFieldErrors(byField);
      setError(formErrors?.join(" ") ?? (Object.keys(byField).length > 0 ? null : apiError.message));
      setSubmitting(false);
    }
  }

  return (
    <section className="max-w-md">
      <h1 className="text-3xl font-bold">Create a student account</h1>
      <p className="mt-2 text-muted">Organizer accounts are set up by the club and can't be created here.</p>
      {error && (
        <div className="mt-6">
          <Alert tone="error">{error}</Alert>
        </div>
      )}
      <form noValidate onSubmit={handleSubmit} className="mt-6 space-y-5">
        <TextField
          label="Full name"
          autoComplete="name"
          required
          maxLength={fieldLimits.fullName}
          value={form.fullName}
          onChange={update("fullName")}
          errors={fieldErrors.fullName}
        />
        <TextField
          label="Student ID"
          hint="Your KYAU student ID. It can't be used by another account."
          required
          maxLength={fieldLimits.studentId}
          value={form.studentId}
          onChange={update("studentId")}
          errors={fieldErrors.studentId}
        />
        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          required
          maxLength={fieldLimits.email}
          value={form.email}
          onChange={update("email")}
          errors={fieldErrors.email}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="new-password"
          hint={`Use ${fieldLimits.passwordMinimum} to ${fieldLimits.passwordMaximum} characters. A few unrelated words work well; no special characters are required.`}
          required
          maxLength={fieldLimits.passwordMaximum}
          value={form.password}
          onChange={update("password")}
          errors={fieldErrors.password}
        />
        <Button type="submit" busy={submitting} className="w-full">
          {submitting ? "Creating account…" : "Create account"}
        </Button>
      </form>
      <p className="mt-6 text-muted">
        Already have an account?{" "}
        <Link to="/sign-in" className="font-semibold text-action hover:text-action-strong">
          Sign in
        </Link>
      </p>
    </section>
  );
}
