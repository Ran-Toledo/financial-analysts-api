/** An HTTP error from the API, with a message fit to show the user. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  title?: unknown
  detail?: unknown
  errors?: unknown
}

/** Picks the most specific message from an RFC 9457 problem body (validation error, then detail, then title). */
export function problemMessage(status: number, body: unknown): string {
  if (status === 429) return 'Too many requests. Wait a minute and try again.'

  if (body && typeof body === 'object') {
    const problem = body as ProblemDetails
    if (problem.errors && typeof problem.errors === 'object') {
      const first = Object.values(problem.errors as Record<string, unknown>).flat()[0]
      if (typeof first === 'string') return first
    }
    if (typeof problem.detail === 'string') return problem.detail
    if (typeof problem.title === 'string') return problem.title
  }

  return `The API returned an error (${status}).`
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof TypeError) return "Couldn't reach the API. Check that it's running."
  return 'Something went wrong.'
}
