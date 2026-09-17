import { useCallback, useEffect, useRef, useState } from 'react'
import { DetailPanel } from './components/DetailPanel'
import { InquiryTable } from './components/InquiryTable'
import { LiveRegions } from './components/LiveRegions'
import { Pagination } from './components/Pagination'
import { ScenarioSwitcher } from './components/ScenarioSwitcher'
import { Toolbar } from './components/Toolbar'
import { fetchInquiry, fetchInquiryPage, putInquiryStatus } from './lib/api'
import { OUTCOME_MESSAGES, messageFor } from './lib/fetchOutcome'
import type { FetchOutcome } from './lib/fetchOutcome'
import type { SortDirection } from './lib/listQuery'
import { applyFilterChange, applyPageChange } from './lib/listState'
import type { ListUiState } from './lib/listState'
import { computeLastPage } from './lib/paging'
import type { DetailState, InquiryPageEnvelope, StatusName } from './lib/types'

type ListPhase =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; envelope: InquiryPageEnvelope }

type RefreshReason = 'save' | 'other'

/**
 * The triage island (ADR-0004/C7): list, filter, paging, detail, and status
 * updates against the same-origin API. Create and delete are API/Swagger
 * operations and intentionally have no controls here. List and detail requests
 * carry sequence guards so an older response never overwrites newer state.
 */
export default function App() {
  const [request, setRequest] = useState<ListUiState>({ filter: 'All' })
  const [sort, setSort] = useState<SortDirection | undefined>(undefined)
  const [phase, setPhase] = useState<ListPhase>({ kind: 'loading' })
  const [fetching, setFetching] = useState(true)
  const [refreshKey, setRefreshKey] = useState(0)
  const [polite, setPolite] = useState('')
  const [assertive, setAssertive] = useState('')
  const [detail, setDetail] = useState<DetailState>({ kind: 'closed' })
  const [mutatingIds, setMutatingIds] = useState<number[]>([])

  const listSeqRef = useRef(0)
  const detailSeqRef = useRef(0)
  const refreshReasonRef = useRef<RefreshReason>('other')
  const phaseRef = useRef<ListPhase>(phase)
  const detailRef = useRef<DetailState>(detail)
  detailRef.current = detail
  const applyPhase = useCallback((next: ListPhase) => {
    phaseRef.current = next
    setPhase(next)
  }, [])

  /** C7: every panel close — user-initiated or programmatic — returns focus to its opener. */
  const focusDetailOpener = useCallback((state: DetailState) => {
    if (state.kind !== 'closed' && state.opener.isConnected) {
      state.opener.focus()
    }
  }, [])

  const refreshList = useCallback((reason: RefreshReason) => {
    refreshReasonRef.current = reason
    setRefreshKey((key) => key + 1)
  }, [])

  // Dev-only: after a scenario is seeded or cleared, return to a clean view of
  // the new dataset (all statuses, first page) and refetch.
  const handleScenarioChanged = useCallback(() => {
    setSort(undefined)
    setRequest({ filter: 'All' })
  }, [])

  useEffect(() => {
    const seq = ++listSeqRef.current
    const controller = new AbortController()
    setFetching(true)

    const applyOutcome = (outcome: FetchOutcome) => {
      const wasSaveRefresh = refreshReasonRef.current === 'save'
      refreshReasonRef.current = 'other'

      if (outcome.kind === 'data') {
        const { envelope } = outcome
        const lastPage = computeLastPage(envelope.page, envelope.totalCount, envelope.pageSize)
        if (envelope.items.length === 0 && envelope.page > lastPage) {
          // The page emptied under pagination (C4 is not a snapshot): land on
          // the last available page and refetch rather than showing a dead end.
          setRequest((current) => ({ ...current, page: lastPage }))
          return
        }
        if (phaseRef.current.kind === 'error') {
          // Only the list-load error clears on recovery; announcements from
          // detail/mutation failures (e.g. record gone) must survive a refresh.
          setAssertive('')
        }
        applyPhase({ kind: 'ready', envelope })
        setFetching(false)
        return
      }

      if (wasSaveRefresh) {
        // The write itself succeeded; only the refresh failed (C7 wording).
        setPolite(OUTCOME_MESSAGES.savedRefreshFailed)
      }
      setAssertive(messageFor(outcome))
      applyPhase(phaseRef.current.kind === 'ready' ? phaseRef.current : { kind: 'error' })
      setFetching(false)
    }

    void (async () => {
      const outcome = await fetchInquiryPage(
        { status: request.filter, page: request.page, sort },
        controller.signal,
      )
      if (seq !== listSeqRef.current) {
        return
      }
      applyOutcome(outcome)
    })()

    return () => controller.abort()
  }, [request, sort, refreshKey])

  const openDetail = useCallback(
    (id: number, opener: HTMLElement) => {
      const seq = ++detailSeqRef.current
      setDetail({ kind: 'loading', id, opener })
      void (async () => {
        const outcome = await fetchInquiry(id)
        if (seq !== detailSeqRef.current) {
          return
        }
        if (outcome.kind === 'record') {
          setDetail((current) =>
            current.kind !== 'closed' && current.id === outcome.inquiry.id
              ? { kind: 'open', id: outcome.inquiry.id, inquiry: outcome.inquiry, opener: current.opener }
              : current,
          )
          return
        }
        setAssertive(messageFor(outcome))
        focusDetailOpener(detailRef.current)
        setDetail({ kind: 'closed' })
        if (outcome.kind === 'gone') {
          refreshList('other')
        }
      })()
    },
    [refreshList, focusDetailOpener],
  )

  const closeDetail = useCallback(() => {
    focusDetailOpener(detailRef.current)
    setDetail({ kind: 'closed' })
  }, [focusDetailOpener])

  const applyStatus = useCallback(
    async (id: number, next: StatusName) => {
      if (mutatingIds.includes(id)) {
        return
      }
      setMutatingIds((current) => [...current, id])
      try {
        const outcome = await putInquiryStatus(id, next)
        if (outcome.kind === 'record') {
          const updated = outcome.inquiry
          if (phaseRef.current.kind === 'ready') {
            const current = phaseRef.current.envelope
            applyPhase({
              kind: 'ready',
              envelope: {
                ...current,
                items: current.items.map((item) => (item.id === updated.id ? updated : item)),
              },
            })
          }
          setDetail((current) =>
            current.kind === 'open' && current.inquiry.id === updated.id
              ? { ...current, inquiry: updated }
              : current,
          )
          setPolite(OUTCOME_MESSAGES.saved)
          refreshList('save')
        } else if (outcome.kind === 'gone') {
          setAssertive(messageFor(outcome))
          const openDetail = detailRef.current
          if (openDetail.kind !== 'closed' && openDetail.id === id) {
            focusDetailOpener(openDetail)
            setDetail({ kind: 'closed' })
          }
          refreshList('other')
        } else {
          // Network/unparseable failures during an update get the mutation
          // wording, not the list-loading message.
          setAssertive(
            outcome.kind === 'generic' ? OUTCOME_MESSAGES.mutationFailure : messageFor(outcome),
          )
        }
      } finally {
        setMutatingIds((current) => current.filter((rowId) => rowId !== id))
      }
    },
    [mutatingIds, refreshList, applyPhase, focusDetailOpener],
  )

  const envelope = phase.kind === 'ready' ? phase.envelope : null
  // The served envelope is authoritative for the displayed page (C4/C7);
  // client-side page state only drives the next request.
  const displayPage = envelope?.page ?? request.page ?? 1
  const lastPage = envelope
    ? computeLastPage(envelope.page, envelope.totalCount, envelope.pageSize)
    : null
  const rows = envelope?.items ?? []

  return (
    <div className="island">
      <ScenarioSwitcher onChanged={handleScenarioChanged} />

      <Toolbar
        filter={request.filter}
        sort={sort}
        totalCount={envelope?.totalCount ?? null}
        onFilterChange={(filter) => setRequest((current) => applyFilterChange(current, filter))}
        onSortChange={(nextSort) => {
          setSort(nextSort)
          setRequest((current) => ({ ...current, page: 1 }))
        }}
      />

      <LiveRegions polite={polite} assertive={assertive} />

      {phase.kind === 'loading' && (
        <p className="list-loading" aria-busy="true">
          Loading inquiries…
        </p>
      )}

      {phase.kind === 'error' && (
        <div className="load-error">
          <p className="load-error-message">{assertive}</p>
          <button type="button" className="retry" onClick={() => refreshList('other')}>
            Retry
          </button>
        </div>
      )}

      {phase.kind === 'ready' && rows.length === 0 && (
        <p className="empty-message">
          {request.filter === 'All'
            ? 'No inquiries yet. New submissions appear here as staff review them.'
            : 'No inquiries match this filter. Choose a different status or show all statuses.'}
        </p>
      )}

      {phase.kind === 'ready' && rows.length > 0 && (
        <InquiryTable
          items={rows}
          fetching={fetching}
          mutatingIds={mutatingIds}
          onOpenDetail={openDetail}
          onApplyStatus={applyStatus}
        />
      )}

      {phase.kind !== 'error' && (
        <Pagination
          page={displayPage}
          lastPage={lastPage}
          onNavigate={(page) => setRequest((current) => applyPageChange(current, page))}
        />
      )}

      <DetailPanel detail={detail} onClose={closeDetail} />
    </div>
  )
}
