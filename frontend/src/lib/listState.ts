import type { StatusName } from './types'

export type StatusFilter = StatusName | 'All'

export interface ListUiState {
  filter: StatusFilter
  /** Omitted until the user navigates or a control resets it (C4 defaults). */
  page?: number
}

/** Changing a filter restarts pagination at page 1 (C7). */
export function applyFilterChange(_state: ListUiState, filter: StatusFilter): ListUiState {
  return { filter, page: 1 }
}

/** Paging preserves the active filter (C7). */
export function applyPageChange(state: ListUiState, page: number): ListUiState {
  return { filter: state.filter, page }
}
