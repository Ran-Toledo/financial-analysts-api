import { describe, expect, it } from 'vitest'
import { ApiError, errorMessage, problemMessage } from './errors'

describe('problemMessage', () => {
  it('prefers the first validation error', () => {
    const body = { title: 'One or more validation errors occurred.', errors: { Sort: ["Unknown sort 'bad'."] } }
    expect(problemMessage(400, body)).toBe("Unknown sort 'bad'.")
  })

  it('falls back to detail, then title, then the status', () => {
    expect(problemMessage(404, { title: 'Not Found', detail: 'No firm with id 9.' })).toBe('No firm with id 9.')
    expect(problemMessage(500, { title: 'Server error' })).toBe('Server error')
    expect(problemMessage(502, 'Bad gateway')).toBe('The API returned an error (502).')
  })

  it('explains rate limiting', () => {
    expect(problemMessage(429, undefined)).toMatch(/Too many requests/)
  })
})

describe('errorMessage', () => {
  it('distinguishes API errors from network failures', () => {
    expect(errorMessage(new ApiError(400, 'Bad horizon'))).toBe('Bad horizon')
    expect(errorMessage(new TypeError('Failed to fetch'))).toMatch(/Couldn't reach the API/)
    expect(errorMessage('weird')).toBe('Something went wrong.')
  })
})
