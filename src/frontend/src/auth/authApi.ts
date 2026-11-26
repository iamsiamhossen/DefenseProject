import { apiRequest } from "../api/client";

export type Role = "Student" | "Organizer";

export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  studentId: string | null;
  roles: Role[];
}

export interface StudentRegistration {
  fullName: string;
  studentId: string;
  email: string;
  password: string;
}

export const authApi = {
  me: () => apiRequest<CurrentUser>("GET", "/api/auth/me"),
  login: (email: string, password: string) => apiRequest<CurrentUser>("POST", "/api/auth/login", { email, password }),
  logout: () => apiRequest<void>("POST", "/api/auth/logout"),
  register: (registration: StudentRegistration) => apiRequest<CurrentUser>("POST", "/api/auth/register", registration),
};
