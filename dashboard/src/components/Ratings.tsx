import type { Schemas } from '../api/client'
import { formatAction, formatPrice } from '../lib/format'

type Rating = Schemas['RatingDto']

const ICONS: Partial<Record<Schemas['RatingAction'], string>> = {
  Upgrade: '▲',
  Downgrade: '▼',
  Initiate: '●',
  TargetRaise: '△',
  TargetLower: '▽',
}

/** The action in words, with an icon colored by direction (the word carries the meaning, not the color). */
export function ActionBadge({ action }: { action: Schemas['RatingAction'] }) {
  return (
    <span className={`action action-${action}`}>
      <span className="action-icon" aria-hidden="true">
        {ICONS[action] ?? '–'}
      </span>
      {formatAction(action)}
    </span>
  )
}

/** "Buy → Neutral", or just the new rating. */
export function RatingChange({ rating }: { rating: Pick<Rating, 'ratingFrom' | 'ratingTo' | 'ratingCarriedForward'> }) {
  const { ratingFrom, ratingTo } = rating
  if (!ratingTo) return <span className="muted">—</span>
  return (
    <span title={rating.ratingCarriedForward ? 'Rating carried forward from the previous call' : undefined}>
      {ratingFrom && ratingFrom !== ratingTo && (
        <>
          <span className="muted">{ratingFrom}</span> →{' '}
        </>
      )}
      {ratingTo}
    </span>
  )
}

/** "$180.00 → $210.00", or the new target alone. */
export function TargetChange({ rating }: { rating: Pick<Rating, 'priceTargetFrom' | 'priceTargetTo'> }) {
  const { priceTargetFrom: from, priceTargetTo: to } = rating
  if (to == null) return <span className="muted">—</span>
  return (
    <span>
      {from != null && from !== to && (
        <>
          <span className="muted">{formatPrice(from)}</span> →{' '}
        </>
      )}
      {formatPrice(to)}
    </span>
  )
}

export function CategoryDot({ category }: { category: string | null | undefined }) {
  if (!category) return null
  return <span className={`dot dot-${category}`} aria-hidden="true" />
}
