/**
 * Last available page for a filtered total: `max(1, ceil(totalCount/pageSize))`
 * (C7). Never 0, so an empty store still has page 1 to land on.
 */
export function computeLastPage(
  _page: number,
  totalCount: number,
  pageSize: number,
): number {
  return Math.max(1, Math.ceil(totalCount / pageSize))
}
