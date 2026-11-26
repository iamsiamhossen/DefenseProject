import { apiRequest } from "../api/client";

export type ProblemType = "Internal" | "Toph";

export interface ProblemSummary {
  id: string;
  label: string;
  title: string;
  type: ProblemType;
  timeLimitMs: number | null;
  memoryLimitKb: number | null;
  sampleCount: number;
  hiddenCount: number;
}

export interface TestCasePreview {
  position: number;
  isSample: boolean;
  inputPreview: string;
  inputLength: number;
  outputPreview: string;
  outputLength: number;
}

export interface ProblemDetail {
  id: string;
  contestId: string;
  label: string;
  title: string;
  type: ProblemType;
  statement: string | null;
  timeLimitMs: number | null;
  memoryLimitKb: number | null;
  externalProblemId: string | null;
  externalProblemUrl: string | null;
  testCases: TestCasePreview[];
}

export interface TestCase {
  position: number;
  isSample: boolean;
  input: string;
  expectedOutput: string;
}

export interface InternalProblemInput {
  label: string;
  title: string;
  statement: string;
  timeLimitMs: number;
  memoryLimitKb: number;
}

export interface TophProblemInput {
  label: string;
  title: string;
  externalProblemId: string;
  externalProblemUrl: string;
}

export interface TestCaseInput {
  input: string;
  expectedOutput: string;
  isSample: boolean;
}

const problems = (contestId: string) => `/api/organizer/contests/${encodeURIComponent(contestId)}/problems`;
const problem = (contestId: string, problemId: string) => `${problems(contestId)}/${encodeURIComponent(problemId)}`;
const testCase = (contestId: string, problemId: string, position: number) => `${problem(contestId, problemId)}/test-cases/${position}`;

export const problemsApi = {
  list: (contestId: string) => apiRequest<ProblemSummary[]>("GET", problems(contestId)),
  get: (contestId: string, problemId: string) => apiRequest<ProblemDetail>("GET", problem(contestId, problemId)),
  createInternal: (contestId: string, input: InternalProblemInput, testCases: TestCaseInput[]) =>
    apiRequest<ProblemDetail>("POST", `${problems(contestId)}/internal`, { ...input, testCases }),
  createToph: (contestId: string, input: TophProblemInput) => apiRequest<ProblemDetail>("POST", `${problems(contestId)}/toph`, input),
  updateInternal: (contestId: string, problemId: string, input: InternalProblemInput) =>
    apiRequest<ProblemDetail>("PUT", `${problem(contestId, problemId)}/internal`, input),
  updateToph: (contestId: string, problemId: string, input: TophProblemInput) =>
    apiRequest<ProblemDetail>("PUT", `${problem(contestId, problemId)}/toph`, input),
  remove: (contestId: string, problemId: string) => apiRequest<void>("DELETE", problem(contestId, problemId)),
  addTestCase: (contestId: string, problemId: string, input: TestCaseInput) =>
    apiRequest<TestCase>("POST", `${problem(contestId, problemId)}/test-cases`, input),
  getTestCase: (contestId: string, problemId: string, position: number) =>
    apiRequest<TestCase>("GET", testCase(contestId, problemId, position)),
  updateTestCase: (contestId: string, problemId: string, position: number, input: Omit<TestCaseInput, "isSample">) =>
    apiRequest<TestCase>("PUT", testCase(contestId, problemId, position), input),
  removeTestCase: (contestId: string, problemId: string, position: number) =>
    apiRequest<void>("DELETE", testCase(contestId, problemId, position)),
};

export function formatLimits(problem: Pick<ProblemSummary, "timeLimitMs" | "memoryLimitKb">): string {
  if (problem.timeLimitMs === null || problem.memoryLimitKb === null) {
    return "";
  }

  const seconds = problem.timeLimitMs / 1000;
  return `${seconds.toLocaleString(undefined, { maximumFractionDigits: 3 })} s, ${(problem.memoryLimitKb / 1024).toLocaleString(undefined, { maximumFractionDigits: 2 })} MB`;
}
