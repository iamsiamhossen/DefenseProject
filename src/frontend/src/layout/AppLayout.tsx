import { useState } from "react";
import { Link, NavLink, Outlet, useNavigate } from "react-router";
import { useAuth } from "../auth/AuthContext";
import { SessionUnavailable } from "../auth/SessionUnavailable";
import { Button, ButtonLink } from "../components/Button";

export function AppLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#main"
        className="sr-only rounded-md bg-surface px-3 py-2 font-semibold focus:not-sr-only focus:absolute focus:top-2 focus:left-2"
      >
        Skip to content
      </a>
      <header className="border-b border-rule bg-surface">
        <div className="mx-auto flex min-h-14 max-w-5xl flex-wrap items-center justify-between gap-x-6 gap-y-2 px-4 py-2">
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
            <Link to="/" className="text-lg font-bold tracking-tight">
              KYAU CP Chapter
            </Link>
            <MainNav />
          </div>
          <AccountNav />
        </div>
      </header>
      <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <SessionUnavailable />
        <Outlet />
      </main>
      <footer className="border-t border-rule">
        <p className="mx-auto max-w-5xl px-4 py-4 text-sm text-muted">Developed for KYAU Programming Club</p>
      </footer>
    </div>
  );
}

const navLinkClasses = ({ isActive }: { isActive: boolean }) =>
  `font-semibold hover:text-action-strong ${isActive ? "text-action underline decoration-2 underline-offset-8" : "text-ink"}`;

function MainNav() {
  const { state } = useAuth();
  const isOrganizer = state.status === "authenticated" && state.user.roles.includes("Organizer");

  return (
    <nav aria-label="Main" className="flex items-center gap-5">
      <NavLink to="/contests" end className={navLinkClasses}>
        Contests
      </NavLink>
      {isOrganizer && (
        <NavLink to="/organizer/contests" className={navLinkClasses}>
          Manage contests
        </NavLink>
      )}
    </nav>
  );
}

function AccountNav() {
  const { state, signOut } = useAuth();
  const navigate = useNavigate();
  const [signingOut, setSigningOut] = useState(false);
  const [error, setError] = useState(false);

  if (state.status === "loading" || state.status === "unavailable") {
    return null;
  }

  if (state.status === "anonymous") {
    return (
      <nav aria-label="Account" className="flex items-center gap-3">
        <NavLink to="/sign-in" className="font-semibold text-action hover:text-action-strong">
          Sign in
        </NavLink>
        <ButtonLink to="/register">Create account</ButtonLink>
      </nav>
    );
  }

  async function handleSignOut() {
    setSigningOut(true);
    setError(false);
    try {
      await signOut();
      navigate("/");
    } catch {
      setError(true);
    } finally {
      setSigningOut(false);
    }
  }

  return (
    <nav aria-label="Account" className="flex flex-wrap items-center gap-x-4 gap-y-1">
      <NavLink to="/account" className="font-semibold hover:text-action-strong">
        {state.user.fullName}
      </NavLink>
      <Button variant="secondary" busy={signingOut} onClick={handleSignOut}>
        {signingOut ? "Signing out…" : "Sign out"}
      </Button>
      {error && (
        <p role="alert" className="w-full text-sm text-danger">
          Couldn't sign you out. Try again.
        </p>
      )}
    </nav>
  );
}
