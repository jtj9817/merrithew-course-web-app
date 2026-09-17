import type { StatusName } from './types'

export type SortDirection = 'createdDateDesc' | 'createdDateAsc'

/** List request state; omitted fields mean server defaults (C4). */
export interface ListQueryState {
  status?: StatusName | '' | 'All'
  page?: number
  pageSize?: number
  sort?: SortDirection
}

/**
 * Serializes list state to a same-origin URL. Omitted fields stay omitted so
 * the server applies its own defaults (C4); values the UI carries explicitly
 * are serialized as-is (a reset to page 1 requests `page=1`; an explicit
 * descending sort requests `sort=createdDateDesc`). An empty-string or `All`
 * filter never serializes (`status=` is invalid per C2).
 */
export function serializeListQuery(state: ListQueryState): string {
  const params = new URLSearchParams()
  if (state.status && state.status !== 'All') {
    params.set('status', state.status)
  }
  if (state.page !== undefined) {
    params.set('page', String(state.page))
  }
  if (state.pageSize !== undefined) {
    params.set('pageSize', String(state.pageSize))
  }
  if (state.sort !== undefined) {
    params.set('sort', state.sort)
  }
  const query = params.toString()
  return query ? `/api/inquiries?${query}` : '/api/inquiries'
}
