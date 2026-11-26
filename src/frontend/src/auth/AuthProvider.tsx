import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { ApiError, resetAntiforgeryToken } from "../api/client";
import { AuthContext, type AuthState } from "./AuthContext";
import { authApi } from "./authApi";

const unavailableMessage = "We couldn't check your session because the server didn't respond as expected.";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: "loading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    authApi
      .me()
      .then((user): AuthState => ({ status: "authenticated", user }))
      .catch((error: unknown): AuthState => toSessionState(error))
      .then((next) => {
        if (active) {
          setState(next);
        }
      });
    return () => {
      active = false;
    };
  }, [attempt]);

  const retry = useCallback(() => {
    setState({ status: "loading" });
    setAttempt((current) => current + 1);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const user = await authApi.login(email, password);
    resetAntiforgeryToken();
    setState({ status: "authenticated", user });
  }, []);

  const signOut = useCallback(async () => {
    try {
      await authApi.logout();
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 401)) {
        throw error;
      }
    }

    resetAntiforgeryToken();
    setState({ status: "anonymous" });
  }, []);

  const value = useMemo(() => ({ state, signIn, signOut, retry }), [state, signIn, signOut, retry]);
  return <AuthContext value={value}>{children}</AuthContext>;
}

function toSessionState(error: unknown): AuthState {
  if (error instanceof ApiError && error.status === 401) {
    return { status: "anonymous" };
  }

  if (error instanceof ApiError && error.status === 0) {
    return { status: "unavailable", message: error.message };
  }

  return { status: "unavailable", message: unavailableMessage };
}
