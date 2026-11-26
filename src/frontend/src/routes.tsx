import type { RouteObject } from "react-router";
import { AccountPage } from "./auth/AccountPage";
import { GuestOnly, RequireAuth } from "./auth/guards";
import { RegisterPage } from "./auth/RegisterPage";
import { SignInPage } from "./auth/SignInPage";
import { StatusPage } from "./components/StatusPage";
import { ContestDetailsPage } from "./contests/ContestDetailsPage";
import { ContestListPage } from "./contests/ContestListPage";
import { HomePage } from "./home/HomePage";
import { AppLayout } from "./layout/AppLayout";
import { ContestFormPage } from "./organizer/ContestFormPage";
import { OrganizerContestPage } from "./organizer/OrganizerContestPage";
import { OrganizerContestsPage } from "./organizer/OrganizerContestsPage";
import { ProblemFormPage } from "./organizer/ProblemFormPage";
import { ProblemPage } from "./organizer/ProblemPage";

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "sign-in", element: <GuestOnly><SignInPage /></GuestOnly> },
      { path: "register", element: <GuestOnly><RegisterPage /></GuestOnly> },
      { path: "account", element: <RequireAuth><AccountPage /></RequireAuth> },
      { path: "contests", element: <ContestListPage /> },
      { path: "contests/:contestId", element: <ContestDetailsPage /> },
      { path: "organizer/contests", element: <RequireAuth role="Organizer"><OrganizerContestsPage /></RequireAuth> },
      { path: "organizer/contests/new", element: <RequireAuth role="Organizer"><ContestFormPage /></RequireAuth> },
      { path: "organizer/contests/:contestId", element: <RequireAuth role="Organizer"><OrganizerContestPage /></RequireAuth> },
      { path: "organizer/contests/:contestId/edit", element: <RequireAuth role="Organizer"><ContestFormPage /></RequireAuth> },
      { path: "organizer/contests/:contestId/problems/new/:type", element: <RequireAuth role="Organizer"><ProblemFormPage /></RequireAuth> },
      { path: "organizer/contests/:contestId/problems/:problemId", element: <RequireAuth role="Organizer"><ProblemPage /></RequireAuth> },
      { path: "organizer/contests/:contestId/problems/:problemId/edit", element: <RequireAuth role="Organizer"><ProblemFormPage /></RequireAuth> },
      { path: "*", element: <StatusPage title="Page not found" message="The page you're looking for doesn't exist or has moved." /> },
    ],
  },
];
