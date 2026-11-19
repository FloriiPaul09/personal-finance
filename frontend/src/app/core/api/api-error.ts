import { HttpErrorResponse } from '@angular/common/http';

/**
 * Codes produced by the app itself, never by the API: a request that got no answer,
 * or an answer that is not a ProblemDetails.
 */
export const CLIENT_ERROR_CODES = {
  /** No answer: offline, the API is down, or the request was blocked. */
  networkUnavailable: 'network.unavailable',
  /**
   * The proxy in front of the API answered instead of the API (502 / 503 / 504 without ProblemDetails):
   * the API is down or restarting. The dev server and nginx answer this way.
   */
  serverUnavailable: 'server.unavailable',
  /** Any other error answer without a ProblemDetails body. */
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

/** Statuses a reverse proxy uses when the API behind it cannot answer. */
const PROXY_STATUSES = [502, 503, 504];

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
    const code = PROXY_STATUSES.includes(response.status)
      ? CLIENT_ERROR_CODES.serverUnavailable
      : CLIENT_ERROR_CODES.unexpectedResponse;
    return new ApiError(response.status, code);
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
 * `errors` by field: the API names each field as the JSON property of the request (camelCase, ADR-0029),
 * so the keys are the names of the form fields.
 */
function fieldErrorsOf(errors: unknown): Record<string, string[]> {
  if (errors === null || typeof errors !== 'object') {
    return {};
  }
  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    result[field] = Array.isArray(messages) ? messages.filter((m) => typeof m === 'string') : [];
  }
  return result;
}
