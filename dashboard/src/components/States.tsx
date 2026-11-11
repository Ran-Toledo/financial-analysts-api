import type { ReactNode } from 'react'
import { errorMessage } from '../lib/errors'

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="state state-error" role="alert">
      <p className="state-title">Couldn't load this</p>
      <p className="state-body">{errorMessage(error)}</p>
      {onRetry && (
        <button type="button" className="button" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="state">
      <p className="state-title">{title}</p>
      {children && <p className="state-body">{children}</p>}
      {action}
    </div>
  )
}
