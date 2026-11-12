import { lazy } from 'react'
import { useParams } from 'react-router'

// The chart libraries only load with the pages that use them.
const ProfilePage = lazy(() => import('./ProfilePage').then((m) => ({ default: m.ProfilePage })))
const StockPage = lazy(() => import('./StockPage').then((m) => ({ default: m.StockPage })))
export const StocksPage = lazy(() => import('./StocksPage').then((m) => ({ default: m.StocksPage })))
export const FeedPage = lazy(() => import('./FeedPage').then((m) => ({ default: m.FeedPage })))

// Keyed by id so moving between profiles or stocks resets their local state (table filter, page).
export function ProfileRoute({ subject }: { subject: 'firms' | 'analysts' }) {
  const { id } = useParams()
  return <ProfilePage key={`${subject}-${id}`} subject={subject} />
}

export function StockRoute() {
  const { ticker } = useParams()
  return <StockPage key={ticker} />
}
