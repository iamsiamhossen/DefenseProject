import { describe, expect, it, vi } from "vitest";
import { installFakeServer, json } from "../test/fakeServer";
import { ApiError, apiRequest, resetAntiforgeryToken } from "./client";

describe("apiRequest", () => {
  it("returns parsed JSON and sends same-origin credentials", async () => {
    installFakeServer({ "GET /api/example": () => json(200, { value: 42 }) });

    const result = await apiRequest<{ value: number }>("GET", "/api/example");

    expect(result).toEqual({ value: 42 });
    expect(vi.mocked(fetch).mock.calls[0]?.[1]?.credentials).toBe("same-origin");
  });

  it("does not request an antiforgery token for GET requests", async () => {
    const calls = installFakeServer({ "GET /api/example": () => json(200, {}) });

    await apiRequest("GET", "/api/example");

    expect(calls.map((call) => call.path)).toEqual(["/api/example"]);
  });

  it("sends the antiforgery token on unsafe requests and reuses it", async () => {
    const calls = installFakeServer({ "POST /api/example": () => json(204) });

    await apiRequest("POST", "/api/example", { a: 1 });
    await apiRequest("POST", "/api/example");

    expect(calls.filter((call) => call.path === "/api/auth/antiforgery")).toHaveLength(1);
    const posts = calls.filter((call) => call.method === "POST");
    expect(posts.map((call) => call.headers["X-XSRF-TOKEN"])).toEqual(["token-1", "token-1"]);
    expect(posts[0]?.body).toEqual({ a: 1 });
    expect(posts[0]?.headers["Content-Type"]).toBe("application/json");
  });

  it("fetches a fresh antiforgery token after a reset", async () => {
    const calls = installFakeServer({ "POST /api/example": () => json(204) });

    await apiRequest("POST", "/api/example");
    resetAntiforgeryToken();
    await apiRequest("POST", "/api/example");

    expect(calls.filter((call) => call.method === "POST").map((call) => call.headers["X-XSRF-TOKEN"])).toEqual(["token-1", "token-2"]);
  });

  it("does not cache a failed antiforgery token request", async () => {
    let attempts = 0;
    installFakeServer({
      "GET /api/auth/antiforgery": () => (++attempts === 1 ? json(500, { title: "Server error" }) : json(200, { requestToken: "token-ok", headerName: "X-XSRF-TOKEN" })),
      "POST /api/example": () => json(204),
    });

    await expect(apiRequest("POST", "/api/example")).rejects.toBeInstanceOf(ApiError);
    await expect(apiRequest("POST", "/api/example")).resolves.toBeUndefined();
  });

  it("returns undefined for 204 responses", async () => {
    installFakeServer({ "POST /api/example": () => json(204) });

    await expect(apiRequest("POST", "/api/example")).resolves.toBeUndefined();
  });

  it("turns problem details into an ApiError with camel-cased field errors", async () => {
    installFakeServer({
      "POST /api/example": () =>
        json(400, {
          title: "One or more validation errors occurred.",
          errors: { Email: ["Email is taken."], StudentId: ["Student ID is taken."], "": ["General problem."] },
        }),
    });

    const error = await apiRequest("POST", "/api/example").catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).message).toBe("One or more validation errors occurred.");
    expect((error as ApiError).fieldErrors).toEqual({
      email: ["Email is taken."],
      studentId: ["Student ID is taken."],
      "": ["General problem."],
    });
  });

  it("prefers the problem detail over the title", async () => {
    installFakeServer({ "POST /api/example": () => json(400, { title: "Bad request", detail: "Registration is closed." }) });

    await expect(apiRequest("POST", "/api/example")).rejects.toThrow("Registration is closed.");
  });

  it("reports network failures as status 0", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new TypeError("Failed to fetch"))));

    const error = await apiRequest("GET", "/api/example").catch((caught: unknown) => caught);

    expect((error as ApiError).status).toBe(0);
    expect((error as ApiError).message).toContain("Can't reach the server");
  });
});
