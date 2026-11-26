export type FieldErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: FieldErrors;

  constructor(status: number, message: string, fieldErrors: FieldErrors = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

type Method = "GET" | "POST" | "PUT" | "DELETE";

interface AntiforgeryToken {
  requestToken: string;
  headerName: string;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

const networkErrorMessage = "Can't reach the server. Check your connection and try again.";

let antiforgeryToken: Promise<AntiforgeryToken> | null = null;

export function resetAntiforgeryToken(): void {
  antiforgeryToken = null;
}

export async function apiRequest<T>(method: Method, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  if (method !== "GET") {
    const token = await getAntiforgeryToken();
    headers[token.headerName] = token.requestToken;
  }

  return send<T>(method, path, headers, body);
}

function getAntiforgeryToken(): Promise<AntiforgeryToken> {
  antiforgeryToken ??= send<AntiforgeryToken>("GET", "/api/auth/antiforgery", { Accept: "application/json" }).catch(
    (error: unknown) => {
      antiforgeryToken = null;
      throw error;
    },
  );
  return antiforgeryToken;
}

async function send<T>(method: Method, path: string, headers: Record<string, string>, body?: unknown): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers,
      credentials: "same-origin",
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, networkErrorMessage);
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
  const message = problem?.detail ?? problem?.title ?? `The request failed with status ${response.status}.`;
  return new ApiError(response.status, message, normalizeFieldErrors(problem?.errors));
}

function normalizeFieldErrors(errors: Record<string, string[]> | undefined): FieldErrors {
  if (!errors) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => [key.charAt(0).toLowerCase() + key.slice(1), messages]),
  );
}
