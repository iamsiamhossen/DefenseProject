import { fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { installFakeServer, json, organizer, renderApp, student, unauthorized } from "../test/fakeServer";
import type { ContestDetails, ContestSummary } from "./contestsApi";

const base: ContestDetails = {
  id: "c-1",
  title: "KYAU Monthly Contest 12",
  description: "Bring your student ID.",
  mode: "Team",
  phase: "Published",
  registrationOpensAt: "2030-01-01T03:00:00Z",
  registrationClosesAt: "2030-01-05T03:00:00Z",
  startsAt: "2030-01-06T03:00:00Z",
  durationMinutes: 300,
  endsAt: "2030-01-06T08:00:00Z",
  freezeAt: "2030-01-06T07:00:00Z",
  isRegistrationOpen: false,
};

const contest = (overrides: Partial<ContestDetails>): ContestDetails => ({ ...base, ...overrides });

describe("public contests", () => {
  it("groups published contests by phase and links to their details", async () => {
    const contests: ContestSummary[] = [
      contest({ id: "u", title: "Upcoming Cup", phase: "Published" }),
      contest({ id: "r", title: "Running Cup", phase: "Running" }),
      contest({ id: "p", title: "Past Cup", phase: "Finalized" }),
    ];
    installFakeServer({ "GET /api/auth/me": unauthorized, "GET /api/contests": () => json(200, contests) });

    renderApp("/contests");

    const running = await screen.findByRole("region", { name: "Running now" });
    expect(within(running).getByRole("link", { name: "Running Cup" }).getAttribute("href")).toBe("/contests/r");
    expect(within(screen.getByRole("region", { name: "Upcoming" })).getByText("Upcoming Cup")).toBeTruthy();
    expect(within(screen.getByRole("region", { name: "Past" })).getByText("Past Cup")).toBeTruthy();
  });

  it("hides groups without contests", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized, "GET /api/contests": () => json(200, [contest({ phase: "Published" })]) });

    renderApp("/contests");

    await screen.findByRole("region", { name: "Upcoming" });
    expect(screen.queryByRole("region", { name: "Running now" })).toBeNull();
    expect(screen.queryByRole("region", { name: "Past" })).toBeNull();
  });

  it("explains when nothing has been published", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized, "GET /api/contests": () => json(200, []) });

    renderApp("/contests");

    expect(await screen.findByText(/No contests have been published yet/)).toBeTruthy();
  });

  it("offers a retry when the list fails to load", async () => {
    let attempts = 0;
    installFakeServer({
      "GET /api/auth/me": unauthorized,
      "GET /api/contests": () => (++attempts === 1 ? json(503, { title: "Service Unavailable" }) : json(200, [contest({})])),
    });
    const user = userEvent.setup();
    renderApp("/contests");

    await user.click(await screen.findByRole("button", { name: "Try again" }));

    expect(await screen.findByText(base.title)).toBeTruthy();
  });

  it("shows contest details with the schedule", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized, "GET /api/contests/c-1": () => json(200, base) });

    renderApp("/contests/c-1");

    expect(await screen.findByRole("heading", { name: base.title })).toBeTruthy();
    expect(screen.getByText("Bring your student ID.")).toBeTruthy();
    expect(screen.getByText("Teams of 1–3")).toBeTruthy();
    expect(screen.getByText("5 h")).toBeTruthy();
    expect(screen.getByText("Upcoming")).toBeTruthy();
    expect(screen.getByText(/Times are shown in your time zone/)).toBeTruthy();
  });

  it("shows a not-found page for unknown or unpublished contests", async () => {
    installFakeServer({ "GET /api/auth/me": unauthorized, "GET /api/contests/missing": () => json(404, { title: "Not Found", status: 404 }) });

    renderApp("/contests/missing");

    expect(await screen.findByRole("heading", { name: "Contest not found" })).toBeTruthy();
  });
});

describe("organizer contests", () => {
  it("shows the management link only to organizers", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(200, student) });

    renderApp("/");

    const nav = await screen.findByRole("navigation", { name: "Main" });
    await screen.findByRole("navigation", { name: "Account" });
    expect(within(nav).queryByRole("link", { name: "Manage contests" })).toBeNull();
    expect(within(nav).getByRole("link", { name: "Contests" })).toBeTruthy();
  });

  it("keeps students out of contest management", async () => {
    installFakeServer({ "GET /api/auth/me": () => json(200, student) });

    renderApp("/organizer/contests/new");

    expect(await screen.findByRole("heading", { name: "You don't have access to this page" })).toBeTruthy();
  });

  it("creates a draft with UTC times and opens it", async () => {
    const created = contest({ id: "new-1", phase: "Draft", title: "Weekly Practice", freezeAt: null });
    const calls = installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "POST /api/organizer/contests": () => json(201, { contest: created, problemCount: 0 }),
      "GET /api/organizer/contests/new-1": () => json(200, { contest: created, problemCount: 0 }),
    });
    const user = userEvent.setup();
    const router = renderApp("/organizer/contests/new");

    await user.type(await screen.findByLabelText("Title"), "Weekly Practice");
    await user.click(screen.getByLabelText("Teams of 1–3"));
    fireEvent.change(screen.getByLabelText("Registration opens"), { target: { value: "2030-01-01T09:00" } });
    fireEvent.change(screen.getByLabelText("Registration closes"), { target: { value: "2030-01-05T09:00" } });
    fireEvent.change(screen.getByLabelText("Contest starts"), { target: { value: "2030-01-06T09:00" } });
    await user.clear(screen.getByLabelText("Duration (minutes)"));
    await user.type(screen.getByLabelText("Duration (minutes)"), "180");
    await user.click(screen.getByRole("button", { name: "Create contest" }));

    expect((await screen.findByText("Contest created as a draft.")).closest("[role=status]")).toBeTruthy();
    expect(router.state.location.pathname).toBe("/organizer/contests/new-1");
    expect(calls.find((call) => call.path === "/api/organizer/contests" && call.method === "POST")?.body).toEqual({
      title: "Weekly Practice",
      description: "",
      mode: "Team",
      registrationOpensAt: new Date("2030-01-01T09:00").toISOString(),
      registrationClosesAt: new Date("2030-01-05T09:00").toISOString(),
      startsAt: new Date("2030-01-06T09:00").toISOString(),
      durationMinutes: 180,
      freezeAt: null,
    });
  });

  it("shows field errors and business-rule errors from the server", async () => {
    let attempt = 0;
    installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "POST /api/organizer/contests": () =>
        ++attempt === 1
          ? json(400, { title: "One or more validation errors occurred.", errors: { Title: ["The Title field is required."] } })
          : json(400, { title: "The request violates a business rule.", detail: "Registration must close no later than the contest start." }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/new");

    await user.click(await screen.findByRole("button", { name: "Create contest" }));
    expect(await screen.findByText("The Title field is required.")).toBeTruthy();
    expect(screen.getByLabelText("Title").getAttribute("aria-invalid")).toBe("true");

    await user.click(screen.getByRole("button", { name: "Create contest" }));
    expect((await screen.findByRole("alert")).textContent).toBe("Registration must close no later than the contest start.");
  });

  it("prefills the edit form and saves with PUT", async () => {
    const draft = contest({ phase: "Draft" });
    const calls = installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "GET /api/organizer/contests/c-1": () => json(200, { contest: draft, problemCount: 0 }),
      "PUT /api/organizer/contests/c-1": () => json(200, { contest: { ...draft, title: "Renamed" }, problemCount: 0 }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/edit");

    const title = await screen.findByLabelText("Title");
    expect((title as HTMLInputElement).value).toBe(base.title);
    await user.clear(title);
    await user.type(title, "Renamed");
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    expect((await screen.findByText("Changes saved.")).closest("[role=status]")).toBeTruthy();
    const put = calls.find((call) => call.method === "PUT");
    expect(put?.body).toMatchObject({ title: "Renamed", mode: "Team", startsAt: new Date(base.startsAt).toISOString(), freezeAt: new Date(base.freezeAt!).toISOString() });
  });

  it("locks schedule times that have already passed on a published contest", async () => {
    const published = contest({
      phase: "Published",
      registrationOpensAt: new Date(Date.now() - 3_600_000).toISOString(),
      registrationClosesAt: new Date(Date.now() + 86_400_000).toISOString(),
      startsAt: new Date(Date.now() + 2 * 86_400_000).toISOString(),
      endsAt: new Date(Date.now() + 2 * 86_400_000 + 18_000_000).toISOString(),
      freezeAt: null,
    });
    installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "GET /api/organizer/contests/c-1": () => json(200, { contest: published, problemCount: 1 }),
    });

    renderApp("/organizer/contests/c-1/edit");

    const opens = await screen.findByLabelText("Registration opens");
    expect((opens as HTMLInputElement).disabled).toBe(true);
    expect(screen.getByText("This time has passed and can't be changed.")).toBeTruthy();
    expect((screen.getByLabelText("Registration closes") as HTMLInputElement).disabled).toBe(false);
  });

  it("sends missing times as null so the server can name the field", async () => {
    const calls = installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "POST /api/organizer/contests": () => json(400, { title: "One or more validation errors occurred.", errors: { StartsAt: ["Enter when the contest starts."] } }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/new");

    await user.click(await screen.findByRole("button", { name: "Create contest" }));

    expect(await screen.findByText("Enter when the contest starts.")).toBeTruthy();
    expect(calls.find((call) => call.method === "POST")?.body).toMatchObject({ registrationOpensAt: null, startsAt: null, freezeAt: null });
  });

  it("does not offer editing once the contest has started", async () => {
    installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "GET /api/organizer/contests/c-1": () => json(200, { contest: contest({ phase: "Running" }), problemCount: 1 }),
    });

    renderApp("/organizer/contests/c-1/edit");

    expect(await screen.findByRole("heading", { name: "This contest can no longer be edited" })).toBeTruthy();
  });

  it("confirms before publishing and reports a blocked publication", async () => {
    const draft = contest({ phase: "Draft" });
    const calls = installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "GET /api/organizer/contests/c-1": () => json(200, { contest: draft, problemCount: 0 }),
      "POST /api/organizer/contests/c-1/publish": () =>
        json(400, { title: "The request violates a business rule.", detail: "A contest needs at least one problem before it can be published." }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1");

    await user.click(await screen.findByRole("button", { name: "Publish…" }));
    expect(calls.some((call) => call.path.endsWith("/publish"))).toBe(false);
    await user.click(screen.getByRole("button", { name: "Publish contest" }));

    expect((await screen.findByRole("alert")).textContent).toBe("A contest needs at least one problem before it can be published.");
    expect(screen.getByText("0 problems")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Publish…" })).toBeTruthy();
  });

  it("shows the published state after a successful publication", async () => {
    const draft = contest({ phase: "Draft" });
    installFakeServer({
      "GET /api/auth/me": () => json(200, organizer),
      "GET /api/organizer/contests/c-1": () => json(200, { contest: draft, problemCount: 1 }),
      "POST /api/organizer/contests/c-1/publish": () => json(200, { contest: { ...draft, phase: "Published" }, problemCount: 1 }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1");

    await user.click(await screen.findByRole("button", { name: "Publish…" }));
    await user.click(screen.getByRole("button", { name: "Publish contest" }));

    expect(await screen.findByText("Upcoming")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Publish" })).toBeNull();
    expect(screen.getByRole("link", { name: "Edit contest" })).toBeTruthy();
  });
});
