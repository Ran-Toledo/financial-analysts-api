import createClient from 'openapi-fetch'
import { ApiError, problemMessage } from '../lib/errors'
import type { components, paths } from './schema'

/** Response and request shapes, generated from the API's Swagger document (`npm run gen:api`). */
export type Schemas = components['schemas']

/** Same origin in dev (Vite proxies /api) and in prod (the API serves the build). */
export const api = createClient<paths>({ baseUrl: '' })

interface FetchResult<T> {
  data?: T
  error?: unknown
  response: Response
}

/** Returns the body of a successful response, or throws an ApiError with a readable message. */
export async function unwrap<T>(request: Promise<FetchResult<T>>): Promise<T> {
  const { data, error, response } = await request
  if (response.ok && data !== undefined) return data
  throw new ApiError(response.status, problemMessage(response.status, error))
}
