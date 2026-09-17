import type { StatusFilter } from '../lib/listState'

interface FilteredStateIndicatorProps {
  filter: StatusFilter
  shownCount: number
  totalCount: number
  onClear: () => void
}

/**
 * Visible filtered-subset indicator (OBS-101 GAP-7): an active status filter
 * must not read as "inquiries are missing". Rendered only for a non-'All'
 * filter, including the empty filtered result; the programmatic announcement
 * of filter changes is owned by the polite live region (A11Y-101 GAP-5).
 */
export function FilteredStateIndicator({
  filter,
  shownCount,
  totalCount,
  onClear,
}: FilteredStateIndicatorProps) {
  if (filter === 'All') {
    return null
  }

  return (
    <div className="filtered-state">
      <p className="filtered-state-text">{`Showing ${shownCount} of ${totalCount} ${
        totalCount === 1 ? 'inquiry' : 'inquiries'
      } · filtered by ${filter}`}</p>
      <button type="button" className="filtered-state-clear" onClick={onClear}>
        Clear filter
      </button>
    </div>
  )
}
