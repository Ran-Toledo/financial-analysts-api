const percent = new Intl.NumberFormat('en-US', {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})
const integer = new Intl.NumberFormat('en-US')
const month = new Intl.DateTimeFormat('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' })

const MISSING = '—'

/** 0.6667 -> "66.7%". */
export function formatPercent(value: number | null | undefined): string {
  return value == null ? MISSING : percent.format(value)
}

/** 0.1509 -> "+15.1%", -0.02 -> "−2.0%", and anything that rounds to zero -> "0.0%". */
export function formatSignedPercent(value: number | null | undefined): string {
  if (value == null) return MISSING
  if (Math.abs(value) < 0.0005) return percent.format(0)
  return (value > 0 ? '+' : '−') + percent.format(Math.abs(value))
}

/** Scores are 0–100 with one decimal. */
export function formatScore(value: number | null | undefined): string {
  return value == null ? MISSING : value.toFixed(1)
}

export function formatCount(value: number): string {
  return integer.format(value)
}

/** "2026-10-31" -> "Oct 2026". */
export function formatMonth(isoDate: string): string {
  return month.format(new Date(`${isoDate}T00:00:00Z`))
}
