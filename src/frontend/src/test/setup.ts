import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";
import { resetAntiforgeryToken } from "../api/client";

beforeEach(() => resetAntiforgeryToken());
afterEach(() => cleanup());
