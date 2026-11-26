export const fieldLimits = {
  fullName: 200,
  studentId: 50,
  email: 256,
  contestTitle: 200,
  problemLabel: 10,
  problemTitle: 200,
  problemStatement: 50_000,
  sampleTestText: 10_000,
  hiddenTestTextBytes: 1_048_576,
  tophProblemId: 100,
  tophProblemUrl: 500,
  passwordMinimum: 15,
  passwordMaximum: 128,
} as const;

const encoder = new TextEncoder();

export function hiddenTestTextErrors(text: string): string[] | undefined {
  return encoder.encode(text).length > fieldLimits.hiddenTestTextBytes ? ["Hidden test data can be at most 1 MB."] : undefined;
}

export const problemLimits = {
  minimumTimeLimitMs: 1_000,
  maximumTimeLimitMs: 15_000,
  minimumMemoryMb: 16,
  maximumMemoryMb: 512,
} as const;
