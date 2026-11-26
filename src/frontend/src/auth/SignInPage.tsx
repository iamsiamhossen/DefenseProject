import { useState, type FormEvent } from "react";
import { Link, useLocation } from "react-router";
import { ApiError, type FieldErrors } from "../api/client";
import { Alert } from "../components/Alert";
import { Button } from "../components/Button";
import { TextField } from "../components/TextField";
import { useAuth } from "./AuthContext";
import { fieldLimits } from "../fieldLimits";

interface SignInLocationState {
  registeredEmail?: string;
}

export function SignInPage() {
  const { signIn } = useAuth();
  const registeredEmail = (useLocation().state as SignInLocationState | null)?.registeredEmail;
  const [email, setEmail] = useState(registeredEmail ?? "");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setFieldErrors({});
    try {
      await signIn(email, password);
    } catch (caught) {
      const apiError = caught instanceof ApiError ? caught : new ApiError(0, "Signing in failed. Try again.");
      setFieldErrors(apiError.fieldErrors);
      setError(Object.keys(apiError.fieldErrors).length > 0 ? null : apiError.message);
      setSubmitting(false);
    }
  }

  return (
    <section className="max-w-md">
      <h1 className="text-3xl font-bold">Sign in</h1>
      <div className="mt-6 space-y-4">
        {registeredEmail && <Alert tone="success">Your account is ready. Sign in to continue.</Alert>}
        {error && <Alert tone="error">{error}</Alert>}
      </div>
      <form noValidate onSubmit={handleSubmit} className="mt-6 space-y-5">
        <TextField
          label="Email"
          type="email"
          autoComplete="username"
          required
          maxLength={fieldLimits.email}
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          errors={fieldErrors.email}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          errors={fieldErrors.password}
        />
        <Button type="submit" busy={submitting} className="w-full">
          {submitting ? "Signing in…" : "Sign in"}
        </Button>
      </form>
      <p className="mt-6 text-muted">
        New to the club?{" "}
        <Link to="/register" className="font-semibold text-action hover:text-action-strong">
          Create a student account
        </Link>
      </p>
    </section>
  );
}
