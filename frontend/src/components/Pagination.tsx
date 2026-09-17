interface PaginationProps {
  page: number
  /** Null while no envelope has arrived yet (bounds unknown). */
  lastPage: number | null
  onNavigate: (page: number) => void
}

export function Pagination({ page, lastPage, onNavigate }: PaginationProps) {
  const atFirstPage = page <= 1
  const atLastPage = lastPage !== null && page >= lastPage

  return (
    <nav className="pagination" aria-label="Pagination">
      <button type="button" disabled={atFirstPage} onClick={() => onNavigate(page - 1)}>
        Previous
      </button>
      <span className="page-indicator" aria-current="page">
        Page {page}
        {lastPage === null ? '' : ` of ${lastPage}`}
      </span>
      <button type="button" disabled={atLastPage} onClick={() => onNavigate(page + 1)}>
        Next
      </button>
    </nav>
  )
}
