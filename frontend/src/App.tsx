import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { ColorblindToggle } from './components/ColorblindToggle'
import { CrmSimulationControl } from './components/CrmSimulationControl'
import { DetailPanel } from './components/DetailPanel'
import { FilteredStateIndicator } from './components/FilteredStateIndicator'
import { InquiryTable } from './components/InquiryTable'
import { IntakeFaultControl } from './components/IntakeFaultControl'
import { LiveRegions, type ToastNotification } from './components/LiveRegions'
import { Pagination } from './components/Pagination'
import { ReconciliationPanel } from './components/ReconciliationPanel'
import { ScenarioSwitcher } from './components/ScenarioSwitcher'
import { StatusBadge } from './components/StatusBadge'
import { Toolbar } from './components/Toolbar'
import { fetchInquiry, fetchInquiryPage, putInquiryStatus } from './lib/api'
import {
  COLORBLIND_EVENT,
  getInitialColorblindMode,
  setColorblindModePreference,
} from './lib/colorblindMode'
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
  const [failedIds, setFailedIds] = useState<number[]>([])
  const [toast, setToast] = useState<ToastNotification | null>(null)
  const [colorblindMode, setColorblindMode] = useState<boolean>(getInitialColorblindMode)
  const [appbarTarget, setAppbarTarget] = useState<HTMLElement | null>(null)

  useEffect(() => {
    setColorblindModePreference(colorblindMode)
  }, [colorblindMode])

  useEffect(() => {
    let target = document.getElementById('shell-appbar-colorblind')
    if (!target) {
      const badgeContainer = document.querySelector('.shell-appbar-badge')
      if (badgeContainer) {
        target = document.createElement('div')
        target.id = 'shell-appbar-colorblind'
        badgeContainer.prepend(target)
      }
    }
    setAppbarTarget(target)
  }, [])

  useEffect(() => {
    const handleExternalChange = (event: Event) => {
      const customEvent = event as CustomEvent<{ enabled: boolean }>
      if (customEvent.detail && typeof customEvent.detail.enabled === 'boolean') {
        setColorblindMode(customEvent.detail.enabled)
      }
    }
    window.addEventListener(COLORBLIND_EVENT, handleExternalChange)
    return () => {
      window.removeEventListener(COLORBLIND_EVENT, handleExternalChange)
    }
  }, [])

  const toggleColorblindMode = useCallback(() => {
    setColorblindMode((current) => {
      const next = !current
      setColorblindModePreference(next)
      setPolite(
        next
          ? 'Colorblind mode enabled. Status badges now display high-discrimination colors, distinct geometric shapes, and semantic icons.'
          : 'Colorblind mode disabled. Standard brand colors restored.',
      )
      return next
    })
  }, [])

  const listSeqRef = useRef(0)
  const detailSeqRef = useRef(0)
  const refreshReasonRef = useRef<RefreshReason>('other')
  const prevRequestRef = useRef<ListUiState | null>(null)
  const phaseRef = useRef<ListPhase>(phase)
  const detailRef = useRef<DetailState>(detail)
  detailRef.current = detail
  const applyPhase = useCallback((next: ListPhase) => {
    phaseRef.current = next
    setPhase(next)
  }, [])

  /** C7: every panel close — user-initiated or programmatic — returns focus to its opener. */
  const focusDetailOpener = useCallback((state: DetailState) => {
    if (state.kind !== 'closed') {
      if (state.opener.isConnected) {
        const opener = state.opener
        opener.focus()
        requestAnimationFrame(() => {
          opener.focus()
        })
      } else {
        const fallback =
          document.getElementById('inquiry-queue') ??
          document.getElementById('status-filter')
        fallback?.focus()
        requestAnimationFrame(() => {
          fallback?.focus()
        })
      }
    }
  }, [])

  const refreshList = useCallback((reason: RefreshReason) => {
    refreshReasonRef.current = reason
    setRefreshKey((key) => key + 1)
  }, [])

  // Dev-only data changes return to a clean view and always refetch, even when
  // the queue already shows the default filter and sort.
  const handleDevelopmentDataChanged = useCallback(() => {
    prevRequestRef.current = null
    setFailedIds([])
    setSort(undefined)
    setRequest({ filter: 'All' })
    refreshList('other')
  }, [refreshList])


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
          if (wasSaveRefresh) {
            refreshReasonRef.current = 'save'
          }
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

        const prevRequest = prevRequestRef.current
        prevRequestRef.current = request

        if (!wasSaveRefresh && prevRequest !== null) {
          const filterChanged = prevRequest.filter !== request.filter
          const pageChanged = (prevRequest.page ?? 1) !== envelope.page
          const countWord = envelope.totalCount === 1 ? 'inquiry' : 'inquiries'

          if (filterChanged) {
            const filterLabel = request.filter === 'All' ? 'All' : request.filter
            setPolite(
              `Filtered by ${filterLabel}: ${envelope.totalCount} matching ${countWord} found`,
            )
          } else if (pageChanged) {
            const pageItemCount = envelope.items.length
            const pageWord = pageItemCount === 1 ? 'inquiry' : 'inquiries'
            setPolite(
              `Page ${envelope.page} (of ${lastPage}) loaded, showing ${pageItemCount} matching ${pageWord}`,
            )
          }
        }
        return
      }

      if (wasSaveRefresh) {
        // The write itself succeeded; only the refresh failed (C7 wording).
        setPolite(OUTCOME_MESSAGES.savedRefreshFailed)
        setToast({ id: Date.now(), variant: 'info' })
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
          setFailedIds((current) => current.filter((rowId) => rowId !== id))
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
          setToast({ id: Date.now(), variant: 'info' })
          refreshList('save')
        } else if (outcome.kind === 'gone') {
          setFailedIds((current) => (current.includes(id) ? current : [...current, id]))
          const message = messageFor(outcome)
          setAssertive(message)
          setToast({ id: Date.now(), variant: 'error' })
          const openDetail = detailRef.current
          if (openDetail.kind !== 'closed' && openDetail.id === id) {
            focusDetailOpener(openDetail)
            setDetail({ kind: 'closed' })
          }
          refreshList('other')
        } else {
          // Network/unparseable failures during an update get the mutation
          // wording, not the list-loading message.
          setFailedIds((current) => (current.includes(id) ? current : [...current, id]))
          const message =
            outcome.kind === 'generic' ? OUTCOME_MESSAGES.mutationFailure : messageFor(outcome)
          setAssertive(message)
          setToast({ id: Date.now(), variant: 'error' })
        }
      } catch {
        setFailedIds((current) => (current.includes(id) ? current : [...current, id]))
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

  const isDetailOpen = detail.kind !== 'closed'

  return (
    <div className="island">
      <div
        className="island-content"
        inert={isDetailOpen ? true : undefined}
      >
        <div className="dev-tools">
          <ScenarioSwitcher onChanged={handleDevelopmentDataChanged} />
          <CrmSimulationControl onInquiryCreated={handleDevelopmentDataChanged} />
          <IntakeFaultControl onChanged={handleDevelopmentDataChanged} />
          <ReconciliationPanel reloadToken={refreshKey} />
        </div>

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

        <section
          id="inquiry-queue"
          tabIndex={-1}
          aria-label="Inquiry queue"
        >
          {phase.kind === 'ready' && (
            <FilteredStateIndicator
              filter={request.filter}
              shownCount={rows.length}
              totalCount={envelope?.totalCount ?? 0}
              onClear={() => setRequest((current) => applyFilterChange(current, 'All'))}
            />
          )}

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
              failedIds={failedIds}
              sort={sort}
              colorblind={colorblindMode}
              onOpenDetail={openDetail}
              onApplyStatus={applyStatus}
            />
          )}
        </section>

        {phase.kind !== 'error' && (
          <Pagination
            page={displayPage}
            lastPage={lastPage}
            onNavigate={(page) => setRequest((current) => applyPageChange(current, page))}
          />
        )}

        <aside className="accessibility-bar" aria-label="Accessibility preferences">
          <div className="accessibility-bar-inner">
            <div className="accessibility-bar-controls">
              <ColorblindToggle
                enabled={colorblindMode}
                onToggle={toggleColorblindMode}
                id="colorblind-toggle-bottom"
                variant="standard"
              />
            </div>
            {colorblindMode && (
              <div className="status-legend" aria-label="Status visual guide">
                <span className="status-legend-title">Colorblind Guide:</span>
                <span className="status-legend-item">
                  <StatusBadge status="New" colorblind={true} />
                  <span className="status-legend-desc">Solid pill + Star</span>
                </span>
                <span className="status-legend-item">
                  <StatusBadge status="Contacted" colorblind={true} />
                  <span className="status-legend-desc">Dashed pill + Chat</span>
                </span>
                <span className="status-legend-item">
                  <StatusBadge status="Pending" colorblind={true} />
                  <span className="status-legend-desc">Dotted rect + Clock</span>
                </span>
                <span className="status-legend-item">
                  <StatusBadge status="Registered" colorblind={true} />
                  <span className="status-legend-desc">Double pill + Check</span>
                </span>
                <span className="status-legend-item">
                  <StatusBadge status="Closed" colorblind={true} />
                  <span className="status-legend-desc">Muted rect + Minus</span>
                </span>
              </div>
            )}
          </div>
        </aside>
      </div>

      <LiveRegions
        polite={polite}
        assertive={assertive}
        toast={toast}
        onDismissToast={() => setToast(null)}
      />

      <DetailPanel detail={detail} colorblind={colorblindMode} onClose={closeDetail} />
      {appbarTarget &&
        createPortal(
          <ColorblindToggle
            enabled={colorblindMode}
            onToggle={toggleColorblindMode}
            id="colorblind-toggle-appbar"
            variant="appbar"
          />,
          appbarTarget,
        )}
    </div>
  )
}
