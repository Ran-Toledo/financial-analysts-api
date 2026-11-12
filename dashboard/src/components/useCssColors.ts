import { useEffect, useState } from 'react'

function read<K extends string>(names: readonly K[]): Record<K, string> {
  const style = getComputedStyle(document.documentElement)
  return Object.fromEntries(names.map((n) => [n, style.getPropertyValue(`--${n}`).trim()])) as Record<K, string>
}

/**
 * Resolved values of CSS color tokens, for canvas charts that can't use var(). Re-reads when the
 * light/dark preference changes, so the chart follows the theme like the rest of the page.
 */
export function useCssColors<K extends string>(names: readonly K[]): Record<K, string> {
  const [colors, setColors] = useState(() => read(names))
  const key = names.join(',')

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    const update = () => setColors(read(key.split(',') as K[]))
    update()
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [key])

  return colors
}
