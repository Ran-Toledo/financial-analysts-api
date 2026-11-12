interface Page<T> {
  items: T[]
  totalPages: number
}

/** Fetches page after page (100 rows each at most) until the last one or `maxPages`, whichever comes first. */
export async function fetchAllPages<T>(
  fetchPage: (page: number) => Promise<Page<T>>,
  maxPages = 5,
): Promise<{ items: T[]; truncated: boolean }> {
  const items: T[] = []
  for (let page = 1; page <= maxPages; page++) {
    const result = await fetchPage(page)
    items.push(...result.items)
    if (page >= result.totalPages) return { items, truncated: false }
  }
  return { items, truncated: true }
}
