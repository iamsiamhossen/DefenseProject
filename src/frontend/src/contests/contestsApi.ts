import { apiRequest } from "../api/client";

export type ContestMode = "Individual" | "Team";
export type ContestPhase = "Draft" | "Published" | "Running" | "Ended" | "Finalized";

export interface ContestSummary {
  id: string;
  title: string;
  mode: ContestMode;
  phase: ContestPhase;
  registrationOpensAt: string;
  registrationClosesAt: string;
  startsAt: string;
  endsAt: string;
  isRegistrationOpen: boolean;
}

export interface ContestDetails extends ContestSummary {
  description: string;
  durationMinutes: number;
  freezeAt: string | null;
}

export interface OrganizerContest {
  contest: ContestDetails;
  problemCount: number;
}

export interface ContestInput {
  title: string;
  description: string;
  mode: ContestMode;
  registrationOpensAt: string | null;
  registrationClosesAt: string | null;
  startsAt: string | null;
  durationMinutes: number;
  freezeAt: string | null;
}

export const contestsApi = {
  list: () => apiRequest<ContestSummary[]>("GET", "/api/contests"),
  get: (id: string) => apiRequest<ContestDetails>("GET", `/api/contests/${encodeURIComponent(id)}`),
};

export const organizerContestsApi = {
  list: () => apiRequest<ContestSummary[]>("GET", "/api/organizer/contests"),
  get: (id: string) => apiRequest<OrganizerContest>("GET", `/api/organizer/contests/${encodeURIComponent(id)}`),
  create: (input: ContestInput) => apiRequest<OrganizerContest>("POST", "/api/organizer/contests", input),
  update: (id: string, input: ContestInput) =>
    apiRequest<OrganizerContest>("PUT", `/api/organizer/contests/${encodeURIComponent(id)}`, input),
  publish: (id: string) => apiRequest<OrganizerContest>("POST", `/api/organizer/contests/${encodeURIComponent(id)}/publish`),
};
