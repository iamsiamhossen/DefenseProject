import type { ContestMode, ContestPhase, ContestSummary } from "./contestsApi";

const dateTimeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export const phaseLabels: Record<ContestPhase, string> = {
  Draft: "Draft",
  Published: "Upcoming",
  Running: "Running",
  Ended: "Ended",
  Finalized: "Results final",
};

export const modeLabels: Record<ContestMode, string> = {
  Individual: "Individual",
  Team: "Teams of 1–3",
};

export function formatDateTime(iso: string): string {
  return dateTimeFormat.format(new Date(iso));
}

export function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) {
    return `${rest} min`;
  }

  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}

export function registrationStatus(contest: ContestSummary, now: Date = new Date()): string {
  if (contest.isRegistrationOpen) {
    return `Registration open until ${formatDateTime(contest.registrationClosesAt)}`;
  }

  if (now < new Date(contest.registrationOpensAt)) {
    return `Registration opens ${formatDateTime(contest.registrationOpensAt)}`;
  }

  return "Registration closed";
}

export function localTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

export function toLocalInput(iso: string | null): string {
  if (!iso) {
    return "";
  }

  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function fromLocalInput(value: string): string | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
