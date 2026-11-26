import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, type RouteObject } from "react-router";
import { vi } from "vitest";
import type { CurrentUser } from "../auth/authApi";
import { AuthProvider } from "../auth/AuthProvider";
import { routes } from "../routes";

export interface FakeCall {
  method: string;
  path: string;
  headers: Record<string, string>;
  body: unknown;
}

type Handler = (call: FakeCall) => Response | Promise<Response>;

export function json(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
  });
}

export const student: CurrentUser = {
  id: "0198b6a0-0000-7000-8000-00000000aaaa",
  email: "anika@students.kyau.test",
  fullName: "Anika Rahman",
  studentId: "221-15-001",
  roles: ["Student"],
};

export const organizer: CurrentUser = {
  id: "0198b6a0-0000-7000-8000-00000000bbbb",
  email: "organizer@kyau.test",
  fullName: "Club Organizer",
  studentId: null,
  roles: ["Organizer"],
};

export const unauthorized = () => json(401, { title: "Unauthorized", status: 401 });

export function installFakeServer(handlers: Record<string, Handler>): FakeCall[] {
  const calls: FakeCall[] = [];
  let tokenNumber = 0;
  const allHandlers: Record<string, Handler> = {
    "GET /api/auth/antiforgery": () => json(200, { requestToken: `token-${++tokenNumber}`, headerName: "X-XSRF-TOKEN" }),
    ...handlers,
  };

  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
      const call: FakeCall = {
        method: init.method ?? "GET",
        path: String(input),
        headers: { ...(init.headers as Record<string, string> | undefined) },
        body: typeof init.body === "string" ? JSON.parse(init.body) : undefined,
      };
      calls.push(call);
      const handler = allHandlers[`${call.method} ${call.path}`];
      return handler ? handler(call) : json(404, { title: "Not Found", status: 404 });
    }),
  );

  return calls;
}

export function renderApp(path: string, routeObjects: RouteObject[] = routes) {
  const router = createMemoryRouter(routeObjects, { initialEntries: [path] });
  render(
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>,
  );
  return router;
}
