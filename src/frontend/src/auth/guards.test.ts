import { describe, expect, it } from "vitest";
import { safeReturnTo } from "./guards";

describe("safeReturnTo", () => {
  it.each(["/account", "/contests/12?tab=standings", "/"])("keeps the internal path %s", (path) => {
    expect(safeReturnTo(path)).toBe(path);
  });

  it.each([null, "", "account", "//evil.example", "/\\evil.example", "https://evil.example", "javascript:alert(1)"])(
    "falls back to the home page for %s",
    (value) => {
      expect(safeReturnTo(value)).toBe("/");
    },
  );
});
