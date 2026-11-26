import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { RequireAuth } from "./auth/guards";
import { installFakeServer, json, organizer, renderApp, student, unauthorized } from "./test/fakeServer";

describe("application shell", () => {
  it("offers sign-in and registration to anonymous visitors", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });

    renderApp("/");

    const nav = await screen.findByRole("navigation", { name: "Account" });
    expect(within(nav).getByRole("link", { name: "Sign in" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Create student account" })).toBeTruthy();
    expect(screen.getByRole("table", { name: "Example standings" })).toBeTruthy();
  });

  it("shows the signed-in user's name in the header", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(200, student) });

    renderApp("/");

    const nav = await screen.findByRole("navigation", { name: "Account" });
    expect(within(nav).getByRole("link", { name: student.fullName })).toBeTruthy();
  });

  it("shows a not-found page for unknown paths", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });

    renderApp("/does-not-exist");

    expect(await screen.findByRole("heading", { name: "Page not found" })).toBeTruthy();
  });
});

describe("session check", () => {
  it("does not treat a server error as signed out", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(500, { title: "An error occurred while processing your request.", status: 500 }) });

    const router = renderApp("/account");

    expect((await screen.findByRole("alert")).textContent).toContain("We couldn't check your session");
    expect(router.state.location.pathname).toBe("/account");
    expect(screen.queryByRole("heading", { name: "Sign in" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "Your account" })).toBeNull();
    expect(screen.queryByRole("navigation", { name: "Account" })).toBeNull();
  });

  it("does not treat a network failure as signed out", async () => {
    installFakeServer({ "GET /api/auth/me": () => Promise.reject(new TypeError("Failed to fetch")) });

    const router = renderApp("/sign-in");

    expect((await screen.findByRole("alert")).textContent).toContain("Can't reach the server");
    expect(router.state.location.pathname).toBe("/sign-in");
    expect(screen.queryByLabelText("Email")).toBeNull();
  });

  it("checks the session again when asked to retry", async () => {
    let attempts = 0;
    installFakeServer({
      "GET /api/auth/me": () => (++attempts === 1 ? json(503, { title: "Service Unavailable", status: 503 }) : json(200, student)),
    });
    const user = userEvent.setup();
    renderApp("/account");

    await user.click(await screen.findByRole("button", { name: "Try again" }));

    expect(await screen.findByRole("heading", { name: "Your account" })).toBeTruthy();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(attempts).toBe(2);
  });

  it("treats only 401 as signed out", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });

    const router = renderApp("/account");

    await screen.findByRole("heading", { name: "Sign in" });
    expect(router.state.location.pathname).toBe("/sign-in");
    expect(screen.queryByText(/couldn't check your session/)).toBeNull();
  });
});

describe("protected routes", () => {
  it("sends anonymous visitors to sign in and returns them afterwards", async () => {
    const calls = installFakeServer({
      "GET /api/auth/me": unauthorized,
      "POST /api/auth/login": () => json(200, student),
    });
    const user = userEvent.setup();
    const router = renderApp("/account");

    await screen.findByRole("heading", { name: "Sign in" });
    expect(router.state.location.pathname).toBe("/sign-in");
    expect(router.state.location.search).toBe("?returnTo=%2Faccount");

    await user.type(screen.getByLabelText("Email"), student.email);
    await user.type(screen.getByLabelText("Password"), "Str0ng!Passw0rd");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("heading", { name: "Your account" })).toBeTruthy();
    expect(router.state.location.pathname).toBe("/account");
    expect(screen.getByText(student.studentId!)).toBeTruthy();

    const login = calls.find((call) => call.path === "/api/auth/login");
    expect(login?.body).toEqual({ email: student.email, password: "Str0ng!Passw0rd" });
    expect(login?.headers["X-XSRF-TOKEN"]).toBe("token-1");
  });

  it("ignores an external return address after sign-in", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(200, student) });

    const router = renderApp("/sign-in?returnTo=%2F%2Fevil.example");

    await waitFor(() => expect(router.state.location.pathname).toBe("/"));
  });

  it("shows a no-access page when the role does not match", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(200, student) });

    renderApp("/organizer", [{ path: "/organizer", element: <RequireAuth role="Organizer"><p>Organizer tools</p></RequireAuth> }]);

    expect(await screen.findByRole("heading", { name: "You don't have access to this page" })).toBeTruthy();
    expect(screen.queryByText("Organizer tools")).toBeNull();
  });

  it("renders role-protected content for the matching role", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(200, organizer) });

    renderApp("/organizer", [{ path: "/organizer", element: <RequireAuth role="Organizer"><p>Organizer tools</p></RequireAuth> }]);

    expect(await screen.findByText("Organizer tools")).toBeTruthy();
  });
});

describe("sign in", () => {
  it("shows the server message when the credentials are rejected", async () => {
    installFakeServer({
      "GET /api/auth/me": unauthorized,
      "POST /api/auth/login": () => json(401, { title: "Invalid email or password.", status: 401 }),
    });
    const user = userEvent.setup();
    const router = renderApp("/sign-in");

    await user.type(await screen.findByLabelText("Email"), student.email);
    await user.type(screen.getByLabelText("Password"), "wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect((await screen.findByRole("alert")).textContent).toBe("Invalid email or password.");
    expect(router.state.location.pathname).toBe("/sign-in");
  });

  it("explains when the server cannot be reached", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });
    const user = userEvent.setup();
    renderApp("/sign-in");
    await screen.findByLabelText("Email");
    installFakeServer({ "POST /api/auth/login": () => Promise.reject(new TypeError("Failed to fetch")) });

    await user.type(screen.getByLabelText("Email"), student.email);
    await user.type(screen.getByLabelText("Password"), "Str0ng!Passw0rd");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect((await screen.findByRole("alert")).textContent).toContain("Can't reach the server");
  });

  it("signs out and uses a fresh antiforgery token for the next request", async () => {
    const calls = installFakeServer({
      "GET /api/auth/me": unauthorized,
      "POST /api/auth/login": () => json(200, student),
      "POST /api/auth/logout": () => json(204),
    });
    const user = userEvent.setup();
    const router = renderApp("/sign-in");

    await user.type(await screen.findByLabelText("Email"), student.email);
    await user.type(screen.getByLabelText("Password"), "Str0ng!Passw0rd");
    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await user.click(await screen.findByRole("button", { name: "Sign out" }));

    const nav = await screen.findByRole("navigation", { name: "Account" });
    expect(await within(nav).findByRole("link", { name: "Sign in" })).toBeTruthy();
    expect(router.state.location.pathname).toBe("/");
    const logout = calls.find((call) => call.path === "/api/auth/logout");
    expect(logout?.headers["X-XSRF-TOKEN"]).toBe("token-2");
  });
});

describe("registration", () => {
  it("creates the account and continues to sign in with the email filled in", async () => {
    const calls = installFakeServer({
      "GET /api/auth/me": unauthorized,
      "POST /api/auth/register": () => json(201, student),
    });
    const user = userEvent.setup();
    const router = renderApp("/register");

    await user.type(await screen.findByLabelText("Full name"), student.fullName);
    await user.type(screen.getByLabelText("Student ID"), student.studentId!);
    await user.type(screen.getByLabelText("Email"), student.email);
    await user.type(screen.getByLabelText("Password"), "Str0ng!Passw0rd");
    await user.click(screen.getByRole("button", { name: "Create account" }));

    expect((await screen.findByRole("status")).textContent).toContain("Your account is ready");
    expect(router.state.location.pathname).toBe("/sign-in");
    expect((screen.getByLabelText("Email") as HTMLInputElement).value).toBe(student.email);
    expect(calls.find((call) => call.path === "/api/auth/register")?.body).toEqual({
      fullName: student.fullName,
      studentId: student.studentId,
      email: student.email,
      password: "Str0ng!Passw0rd",
    });
  });

  it("shows server validation errors next to the matching fields", async () => {
    installFakeServer({
      "GET /api/auth/me": unauthorized,
      "POST /api/auth/register": () =>
        json(400, {
          title: "One or more validation errors occurred.",
          errors: {
            StudentId: ["An account with this student ID already exists."],
            Password: ["Passwords must have at least one digit ('0'-'9')."],
          },
        }),
    });
    const user = userEvent.setup();
    renderApp("/register");

    await user.click(await screen.findByRole("button", { name: "Create account" }));

    const studentId = await screen.findByLabelText("Student ID");
    await waitFor(() => expect(studentId.getAttribute("aria-invalid")).toBe("true"));
    expect(screen.getByText("An account with this student ID already exists.")).toBeTruthy();
    expect(screen.getByLabelText("Password").getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByLabelText("Email").getAttribute("aria-invalid")).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("keeps the recorded field limits on the inputs", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });

    renderApp("/register");

    expect((await screen.findByLabelText("Full name")).getAttribute("maxlength")).toBe("200");
    expect(screen.getByLabelText("Student ID").getAttribute("maxlength")).toBe("50");
    expect(screen.getByLabelText("Email").getAttribute("maxlength")).toBe("256");
    expect(screen.getByLabelText("Password").getAttribute("maxlength")).toBe("128");
  });

  it("describes the password policy", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });

    renderApp("/register");

    const password = await screen.findByLabelText("Password");
    const hintId = password.getAttribute("aria-describedby");
    expect(document.getElementById(hintId!)?.textContent).toBe(
      "Use 15 to 128 characters. A few unrelated words work well; no special characters are required.",
    );
  });

  it("does not limit the sign-in password so older accounts keep working", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized });

    renderApp("/sign-in");

    expect((await screen.findByLabelText("Password")).getAttribute("maxlength")).toBeNull();
  });
});
