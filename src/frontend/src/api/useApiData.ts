import { useCallback, useEffect, useState, type DependencyList } from "react";
import { ApiError } from "./client";

export type ApiData<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; error: ApiError };

export function useApiData<T>(load: () => Promise<T>, deps: DependencyList) {
  const [state, setState] = useState<ApiData<T>>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    setState({ status: "loading" });
    load()
      .then((data): ApiData<T> => ({ status: "ready", data }))
      .catch((error: unknown): ApiData<T> => ({
        status: "error",
        error: error instanceof ApiError ? error : new ApiError(0, "Something went wrong. Try again."),
      }))
      .then((next) => {
        if (active) {
          setState(next);
        }
      });
    return () => {
      active = false;
    };
  }, [...deps, attempt]);

  const reload = useCallback(() => setAttempt((current) => current + 1), []);
  const replace = useCallback((data: T) => setState({ status: "ready", data }), []);

  return { state, reload, replace };
}
