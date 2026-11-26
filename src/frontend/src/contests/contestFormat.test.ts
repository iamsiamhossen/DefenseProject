import { describe, expect, it } from "vitest";
import { formatDuration, fromLocalInput, registrationStatus, toLocalInput } from "./contestFormat";
import type { ContestSummary } from "./contestsApi";

describe("contest formatting", () => {
  it.each([
    [45, "45 min"],
    [60, "1 h"],
    [150, "2 h 30 min"],
    [300, "5 h"],
  ])("formats %i minutes as %s", (minutes, expected) => {
    expect(formatDuration(minutes)).toBe(expected);
  });

  it("round-trips a UTC time through the local date-time input", () => {
    const iso = "2030-01-10T03:30:00.000Z";

    expect(fromLocalInput(toLocalInput(iso))).toBe(iso);
  });

  it("treats an empty or invalid local input as missing", () => {
    expect(fromLocalInput("")).toBeNull();
    expect(fromLocalInput("not a date")).toBeNull();
    expect(toLocalInput(null)).toBe("");
  });

  describe("registration status", () => {
    const contest: ContestSummary = {
      id: "1",
      title: "Contest",
      mode: "Individual",
      phase: "Published",
      registrationOpensAt: "2030-01-01T00:00:00Z",
      registrationClosesAt: "2030-01-05T00:00:00Z",
      startsAt: "2030-01-06T00:00:00Z",
      endsAt: "2030-01-06T05:00:00Z",
      isRegistrationOpen: false,
    };

    it("says when registration opens before the window", () => {
      expect(registrationStatus(contest, new Date("2029-12-31T00:00:00Z"))).toMatch(/^Registration opens /);
    });

    it("says when registration closes while it is open", () => {
      expect(registrationStatus({ ...contest, isRegistrationOpen: true }, new Date("2030-01-02T00:00:00Z"))).toMatch(
        /^Registration open until /,
      );
    });

    it("says registration is closed after the window", () => {
      expect(registrationStatus(contest, new Date("2030-01-05T00:00:00Z"))).toBe("Registration closed");
    });
  });
});
