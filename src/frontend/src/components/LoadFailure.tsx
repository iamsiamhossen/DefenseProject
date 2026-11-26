import type { ApiError } from "../api/client";
import { Alert } from "./Alert";
import { Button } from "./Button";
import { StatusPage } from "./StatusPage";

interface LoadFailureProps {
  error: ApiError;
  onRetry: () => void;
  notFoundTitle?: string;
}

export function LoadFailure({ error, onRetry, notFoundTitle = "Not found" }: LoadFailureProps) {
  if (error.status === 404) {
    return <StatusPage title={notFoundTitle} message="It doesn't exist, or it isn't available to you." />;
  }

  return (
    <Alert tone="error">
      <p>{error.message}</p>
      <Button variant="secondary" className="mt-3" onClick={onRetry}>
        Try again
      </Button>
    </Alert>
  );
}
