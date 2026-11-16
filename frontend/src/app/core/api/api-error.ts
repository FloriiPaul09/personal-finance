import { HttpErrorResponse } from '@angular/common/http';

/**
 * Codes produced by the app itself, never by the API: a request that got no answer,
 * or an answer that is not a ProblemDetails.
 */
export const CLIENT_ERROR_CODES = {
  /** No answer: offline, the API is down, or the request was blocked. */
  networkUnavailable: 'network.unavailable',
  /** An error answer without a ProblemDetails body (e.g. a proxy page). */
  unexpectedResponse: 'response.unexpected',
} as const;

/**
 * An error answer of the API, in one shape for the whole app
 * (docs/backend/validation-and-errors.md#response-shape).
 * Pages switch on `code`; form fields read `fieldErrors`; `traceId` identifies the request in the logs.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly fieldErrors: Readonly<Record<string, readonly string[]>> = {},
    readonly traceId: string | null = null,
    readonly detail: string | null = null,
  ) {
    super(`${status} ${code}${detail ? `: ${detail}` : ''}`);
    this.name = 'ApiError';
  }

  /** Fields the API rejected, by the request property name (camelCase). */
  get fields(): string[] {
    return Object.keys(this.fieldErrors);
  }

  hasCode(...codes: string[]): boolean {
    return codes.includes(this.code);
  }
}

interface ProblemDetails {
  status?: number;
  code?: unknown;
  detail?: unknown;
  traceId?: unknown;
  errors?: unknown;
}

/** Turns an `HttpErrorResponse` into an `ApiError`, whatever its body. */
export function toApiError(response: HttpErrorResponse): ApiError {
  if (response.status === 0) {
    return new ApiError(0, CLIENT_ERROR_CODES.networkUnavailable);
  }

  const problem = asProblem(response.error);
  if (!problem || typeof problem.code !== 'string') {
    return new ApiError(response.status, CLIENT_ERROR_CODES.unexpectedResponse);
  }

  return new ApiError(
    response.status,
    problem.code,
    fieldErrorsOf(problem.errors),
    typeof problem.traceId === 'string' ? problem.traceId : null,
    typeof problem.detail === 'string' ? problem.detail : null,
  );
}

function asProblem(body: unknown): ProblemDetails | null {
  if (typeof body === 'string') {
    try {
      body = JSON.parse(body);
    } catch {
      return null;
    }
  }
  return body !== null && typeof body === 'object' ? (body as ProblemDetails) : null;
}

/**
 * `errors` by field, with keys in camelCase like the request properties:
 * the API may name a field after the C# property (`AccountId`).
 */
function fieldErrorsOf(errors: unknown): Record<string, string[]> {
  if (errors === null || typeof errors !== 'object') {
    return {};
  }
  const result: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(errors)) {
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    const list = Array.isArray(messages) ? messages.filter((m) => typeof m === 'string') : [];
    result[field] = [...(result[field] ?? []), ...list];
  }
  return result;
}
