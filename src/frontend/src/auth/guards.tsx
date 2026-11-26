import type { ReactNode } from "react";
import { Navigate, useLocation, useSearchParams } from "react-router";
import { LoadingState } from "../components/LoadingState";
import { StatusPage } from "../components/StatusPage";
import { useAuth } from "./AuthContext";
import type { Role } from "./authApi";

export function RequireAuth({ role, children }: { role?: Role; children: ReactNode }) {
  const { state } = useAuth();
  const location = useLocation();

  if (state.status === "loading") {
    return <LoadingState label="Checking your session" />;
  }

  if (state.status === "unavailable") {
    return null;
  }

  if (state.status === "anonymous") {
    const returnTo = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/sign-in?returnTo=${returnTo}`} replace />;
  }

  if (role && !state.user.roles.includes(role)) {
    return (
      <StatusPage
        title="You don't have access to this page"
        message="Your account doesn't have permission to open it. If you think this is wrong, contact a club organizer."
      />
    );
  }

  return children;
}

export function GuestOnly({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const [searchParams] = useSearchParams();

  if (state.status === "loading") {
    return <LoadingState label="Checking your session" />;
  }

  if (state.status === "unavailable") {
    return null;
  }

  if (state.status === "authenticated") {
    return <Navigate to={safeReturnTo(searchParams.get("returnTo"))} replace />;
  }

  return children;
}

export function safeReturnTo(value: string | null): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) {
    return "/";
  }

  return value;
}
