import { Alert } from "../components/Alert";
import { Button } from "../components/Button";
import { useAuth } from "./AuthContext";

export function SessionUnavailable() {
  const { state, retry } = useAuth();
  if (state.status !== "unavailable") {
    return null;
  }

  return (
    <div className="mb-8">
      <Alert tone="error">
        <p>{state.message}</p>
        <Button variant="secondary" className="mt-3" onClick={retry}>
          Try again
        </Button>
      </Alert>
    </div>
  );
}
