import { isRouteErrorResponse, Link, useRouteError } from 'react-router'
import { EmptyState } from '../components/States'

export function NotFoundPage() {
  return (
    <EmptyState title="Page not found" action={<Link className="button" to="/">Go to the leaderboard</Link>}>
      There's nothing at this address.
    </EmptyState>
  )
}

/** Shown when rendering a route throws. */
export function RouteErrorPage() {
  const error = useRouteError()
  if (isRouteErrorResponse(error) && error.status === 404) return <NotFoundPage />
  return (
    <main className="container">
      <EmptyState title="Something went wrong" action={<Link className="button" to="/">Go to the leaderboard</Link>}>
        {error instanceof Error ? error.message : 'The page failed to load.'}
      </EmptyState>
    </main>
  )
}
