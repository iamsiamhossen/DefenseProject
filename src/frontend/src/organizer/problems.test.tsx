import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import type { ContestDetails, ContestPhase } from "../contests/contestsApi";
import type { ProblemDetail, ProblemSummary } from "../problems/problemsApi";
import { installFakeServer, json, organizer, renderApp } from "../test/fakeServer";

const contest = (phase: ContestPhase): ContestDetails => ({
  id: "c-1",
  title: "KYAU Monthly Contest 12",
  description: "",
  mode: "Team",
  phase,
  registrationOpensAt: "2030-01-01T03:00:00Z",
  registrationClosesAt: "2030-01-05T03:00:00Z",
  startsAt: "2030-01-06T03:00:00Z",
  durationMinutes: 300,
  endsAt: "2030-01-06T08:00:00Z",
  freezeAt: null,
  isRegistrationOpen: false,
});

const internalProblem: ProblemDetail = {
  id: "p-1",
  contestId: "c-1",
  label: "A",
  title: "Sum of Two",
  type: "Internal",
  statement: "Read two integers.\nPrint their sum.",
  timeLimitMs: 2000,
  memoryLimitKb: 262144,
  externalProblemId: null,
  externalProblemUrl: null,
  testCases: [
    { position: 1, isSample: true, inputPreview: "1 2", inputLength: 3, outputPreview: "3", outputLength: 1 },
    { position: 2, isSample: false, inputPreview: "x".repeat(300), inputLength: 5000, outputPreview: "y", outputLength: 1 },
  ],
};

const tophProblem: ProblemDetail = {
  ...internalProblem,
  id: "p-2",
  label: "B",
  title: "Add Them Up",
  type: "Toph",
  statement: null,
  timeLimitMs: null,
  memoryLimitKb: null,
  externalProblemId: "add-them-up",
  externalProblemUrl: "https://toph.co/p/add-them-up",
  testCases: [],
};

const summaries: ProblemSummary[] = [
  { id: "p-1", label: "A", title: "Sum of Two", type: "Internal", timeLimitMs: 2000, memoryLimitKb: 262144, sampleCount: 1, hiddenCount: 1 },
  { id: "p-2", label: "B", title: "Add Them Up", type: "Toph", timeLimitMs: null, memoryLimitKb: null, sampleCount: 0, hiddenCount: 0 },
];

function server(phase: ContestPhase, extra: Parameters<typeof installFakeServer>[0] = {}) {
  return installFakeServer({
    "GET /api/auth/me": () => json(200, organizer),
    "GET /api/organizer/contests/c-1": () => json(200, { contest: contest(phase), problemCount: 2 }),
    "GET /api/organizer/contests/c-1/problems": () => json(200, summaries),
    "GET /api/organizer/contests/c-1/problems/p-1": () => json(200, internalProblem),
    "GET /api/organizer/contests/c-1/problems/p-2": () => json(200, tophProblem),
    ...extra,
  });
}

describe("contest problems list", () => {
  it("lists problems with their type, limits and test case counts", async () => {
    server("Draft");

    renderApp("/organizer/contests/c-1");

    const table = await screen.findByRole("table", { name: "Contest problems" });
    const rows = within(table).getAllByRole("row");
    expect(within(rows[1]!).getByRole("link", { name: "Sum of Two" }).getAttribute("href")).toBe("/organizer/contests/c-1/problems/p-1");
    expect(within(rows[1]!).getByText("1 sample, 1 hidden")).toBeTruthy();
    expect(within(rows[2]!).getByText("Toph reference")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Add internal problem" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Add Toph problem" })).toBeTruthy();
  });

  it("does not offer changes once the contest is running", async () => {
    server("Running");

    renderApp("/organizer/contests/c-1");

    await screen.findByRole("table", { name: "Contest problems" });
    expect(screen.queryByRole("link", { name: "Add internal problem" })).toBeNull();
  });
});

describe("problem forms", () => {
  it("creates an internal problem for a draft and converts memory to KB", async () => {
    const calls = server("Draft", {
      "POST /api/organizer/contests/c-1/problems/internal": () => json(201, internalProblem),
    });
    const user = userEvent.setup();
    const router = renderApp("/organizer/contests/c-1/problems/new/internal");

    await user.type(await screen.findByLabelText("Label"), "A");
    await user.type(screen.getByLabelText("Title"), "Sum of Two");
    await user.type(screen.getByLabelText("Statement"), "Print a + b.");
    await user.clear(screen.getByLabelText("Time limit (ms)"));
    await user.type(screen.getByLabelText("Time limit (ms)"), "2000");
    await user.clear(screen.getByLabelText("Memory limit (MB)"));
    await user.type(screen.getByLabelText("Memory limit (MB)"), "64");
    await user.click(screen.getByRole("button", { name: "Add problem" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/organizer/contests/c-1/problems/p-1"));
    expect(calls.find((call) => call.method === "POST")?.body).toEqual({
      label: "A",
      title: "Sum of Two",
      statement: "Print a + b.",
      timeLimitMs: 2000,
      memoryLimitKb: 65536,
      testCases: [],
    });
  });

  it("requires a first hidden test case when the contest is published", async () => {
    const calls = server("Published", {
      "POST /api/organizer/contests/c-1/problems/internal": () =>
        json(400, { title: "One or more validation errors occurred.", errors: { "TestCases[0].ExpectedOutput": ["Provide the expected output."] } }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/new/internal");

    expect(await screen.findByText(/must start with a hidden test case/)).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();
    await user.type(screen.getByLabelText("Input"), "1 2");
    await user.click(screen.getByRole("button", { name: "Add problem" }));

    expect(await screen.findByText("Provide the expected output.")).toBeTruthy();
    expect(calls.find((call) => call.method === "POST")?.body).toMatchObject({ testCases: [{ input: "1 2", expectedOutput: "", isSample: false }] });
  });

  it("creates a Toph problem reference", async () => {
    const calls = server("Draft", {
      "POST /api/organizer/contests/c-1/problems/toph": () => json(201, tophProblem),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/new/toph");

    await user.type(await screen.findByLabelText("Label"), "B");
    await user.type(screen.getByLabelText("Title"), "Add Them Up");
    await user.type(screen.getByLabelText("Toph problem ID"), "add-them-up");
    await user.type(screen.getByLabelText("Toph problem URL"), "https://toph.co/p/add-them-up");
    await user.click(screen.getByRole("button", { name: "Add problem" }));

    expect(await screen.findByText("Toph reference")).toBeTruthy();
    expect(calls.find((call) => call.method === "POST")?.body).toEqual({
      label: "B",
      title: "Add Them Up",
      externalProblemId: "add-them-up",
      externalProblemUrl: "https://toph.co/p/add-them-up",
    });
  });

  it("prefills the edit form and saves with PUT", async () => {
    const calls = server("Draft", {
      "PUT /api/organizer/contests/c-1/problems/p-1/internal": () => json(200, { ...internalProblem, title: "Renamed" }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/p-1/edit");

    const title = await screen.findByLabelText("Title");
    expect((screen.getByLabelText("Memory limit (MB)") as HTMLInputElement).value).toBe("256");
    expect(screen.queryByText("First hidden test case")).toBeNull();
    await user.clear(title);
    await user.type(title, "Renamed");
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByText("Problem saved.")).toBeTruthy();
    expect(calls.find((call) => call.method === "PUT")?.body).toEqual({
      label: "A",
      title: "Renamed",
      statement: internalProblem.statement,
      timeLimitMs: 2000,
      memoryLimitKb: 262144,
    });
  });
});

describe("problem page", () => {
  it("shows the statement, limits and separated sample and hidden test cases", async () => {
    server("Draft");

    renderApp("/organizer/contests/c-1/problems/p-1");

    expect(await screen.findByRole("heading", { name: /Sum of Two/ })).toBeTruthy();
    expect(screen.getByText("2 s, 256 MB")).toBeTruthy();
    expect(screen.getByText(/Read two integers\./).textContent).toBe("Read two integers.\nPrint their sum.");
    const samples = screen.getByRole("region", { name: "Samples" });
    const hidden = screen.getByRole("region", { name: "Hidden test cases" });
    expect(within(samples).getByText("Sample 1")).toBeTruthy();
    expect(within(hidden).getByText("Hidden test 1")).toBeTruthy();
    expect(within(hidden).getByText(/first 300 of 5,000 characters/)).toBeTruthy();
  });

  it("adds a sample with the sample flag", async () => {
    const calls = server("Draft", {
      "POST /api/organizer/contests/c-1/problems/p-1/test-cases": () => json(201, { position: 3, isSample: true, input: "4 5", expectedOutput: "9" }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/p-1");

    await user.click(await screen.findByRole("button", { name: "Add sample" }));
    const form = screen.getByRole("form", { name: "New sample" });
    await user.type(within(form).getByLabelText("Input"), "4 5");
    await user.type(within(form).getByLabelText("Expected output"), "9");
    await user.click(within(form).getByRole("button", { name: "Add sample" }));

    await waitFor(() => expect(calls.filter((call) => call.path.endsWith("/problems/p-1")).length).toBe(2));
    expect(calls.find((call) => call.method === "POST")?.body).toEqual({ input: "4 5", expectedOutput: "9", isSample: true });
  });

  it("limits sample fields to 10,000 characters", async () => {
    server("Draft");
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/p-1");

    await user.click(await screen.findByRole("button", { name: "Add sample" }));
    const form = screen.getByRole("form", { name: "New sample" });

    expect(within(form).getByLabelText("Input").getAttribute("maxlength")).toBe("10000");
    expect(within(form).getByLabelText("Expected output").getAttribute("maxlength")).toBe("10000");
  });

  it("stops hidden data over 1 MB, measured in UTF-8 bytes, before sending", async () => {
    const calls = server("Draft");
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/p-1");

    await user.click(await screen.findByRole("button", { name: "Add hidden test case" }));
    const form = screen.getByRole("form", { name: "New hidden test case" });
    const twoByteText = "é".repeat(524_289);
    expect(twoByteText.length).toBeLessThan(1_048_576);
    fireEvent.change(within(form).getByLabelText("Input"), { target: { value: twoByteText } });
    expect(within(form).getByLabelText("Input").getAttribute("maxlength")).toBeNull();
    await user.click(within(form).getByRole("button", { name: "Add hidden test case" }));

    expect(await within(form).findByText("Hidden test data can be at most 1 MB.")).toBeTruthy();
    expect(calls.some((call) => call.method === "POST")).toBe(false);
  });

  it("loads the full test case before editing it", async () => {
    const calls = server("Draft", {
      "GET /api/organizer/contests/c-1/problems/p-1/test-cases/2": () => json(200, { position: 2, isSample: false, input: "x".repeat(5000), expectedOutput: "y" }),
      "PUT /api/organizer/contests/c-1/problems/p-1/test-cases/2": () => json(200, { position: 2, isSample: false, input: "z", expectedOutput: "y" }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/p-1");

    const hidden = await screen.findByRole("region", { name: "Hidden test cases" });
    await user.click(within(hidden).getByRole("button", { name: "Edit" }));
    const form = await screen.findByRole("form", { name: "Edit Hidden test 1" });
    const input = (await within(form).findByLabelText("Input")) as HTMLTextAreaElement;
    expect(input.value).toHaveLength(5000);
    await user.clear(input);
    await user.type(input, "z");
    await user.click(within(form).getByRole("button", { name: "Save test case" }));

    await waitFor(() => expect(calls.some((call) => call.method === "PUT")).toBe(true));
    expect(calls.find((call) => call.method === "PUT")?.body).toEqual({ input: "z", expectedOutput: "y" });
  });

  it("confirms before deleting a test case and shows a blocked deletion", async () => {
    server("Published", {
      "DELETE /api/organizer/contests/c-1/problems/p-1/test-cases/2": () =>
        json(400, { title: "The request violates a business rule.", detail: "An internal problem in a published contest must keep at least one hidden test case." }),
    });
    const user = userEvent.setup();
    renderApp("/organizer/contests/c-1/problems/p-1");

    const hidden = await screen.findByRole("region", { name: "Hidden test cases" });
    await user.click(within(hidden).getByRole("button", { name: "Delete…" }));
    await user.click(within(hidden).getByRole("button", { name: "Delete test case" }));

    expect((await within(hidden).findByRole("alert")).textContent).toBe("An internal problem in a published contest must keep at least one hidden test case.");
  });

  it("warns when there is no hidden test case yet", async () => {
    server("Draft", {
      "GET /api/organizer/contests/c-1/problems/p-1": () => json(200, { ...internalProblem, testCases: [internalProblem.testCases[0]] }),
    });

    renderApp("/organizer/contests/c-1/problems/p-1");

    expect(await screen.findByText("Add at least one hidden test case before the contest can be published.")).toBeTruthy();
  });

  it("shows a Toph reference only to the organizer, with the participant note", async () => {
    server("Draft");

    renderApp("/organizer/contests/c-1/problems/p-2");

    expect(await screen.findByText("add-them-up")).toBeTruthy();
    expect(screen.getByText(/Participants can't see a statement for this problem yet/)).toBeTruthy();
    expect(screen.queryByRole("region", { name: "Samples" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "Statement" })).toBeNull();
  });

  it("locks the problem once the contest has started", async () => {
    server("Running");

    renderApp("/organizer/contests/c-1/problems/p-1");

    expect(await screen.findByText("This contest has started, so its problems are locked.")).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Edit problem" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add sample" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Delete…" })).toBeNull();
  });
});
