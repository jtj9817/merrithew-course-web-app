import type { SortDirection } from '../lib/listQuery'
import type { StatusFilter } from '../lib/listState'
import { STATUS_NAMES } from '../lib/types'

interface ToolbarProps {
  filter: StatusFilter
  sort: SortDirection | undefined
  totalCount: number | null
  onFilterChange: (filter: StatusFilter) => void
  onSortChange: (sort: SortDirection) => void
}

export function Toolbar({
  filter,
  sort,
  totalCount,
  onFilterChange,
  onSortChange,
}: ToolbarProps) {
  return (
    <div className="toolbar">
      <div className="toolbar-control">
        <label htmlFor="status-filter" className="toolbar-label">
          Filter by status
        </label>
        <select
          id="status-filter"
          value={filter}
          onChange={(event) => onFilterChange(event.target.value as StatusFilter)}
        >
          <option value="All">All statuses</option>
          {STATUS_NAMES.map((status) => (
            <option key={status} value={status}>
              {status}
            </option>
          ))}
        </select>
      </div>

      <div className="toolbar-control">
        <label htmlFor="sort-order" className="toolbar-label">
          Sort order
        </label>
        <select
          id="sort-order"
          value={sort ?? 'createdDateDesc'}
          onChange={(event) => onSortChange(event.target.value as SortDirection)}
        >
          <option value="createdDateDesc">Newest first</option>
          <option value="createdDateAsc">Oldest first</option>
        </select>
      </div>

      {totalCount !== null && (
        <p className="total-count">
          {totalCount} {totalCount === 1 ? 'inquiry' : 'inquiries'}
        </p>
      )}
    </div>
  )
}
