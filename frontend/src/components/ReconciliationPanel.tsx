import { useEffect, useState } from 'react'
import { fetchReconciliation, reconciliationToolsEnabled } from '../lib/reconciliation'
import type { ReconciliationReport } from '../lib/reconciliation'
import { Spinner } from './Progress'

interface ReconciliationPanelProps {
  /** Refetches whenever this token changes (e.g. after a scenario or status change). */
  reloadToken?: number
}

/**
 * Dev-only, read-only reconciliation readout: the count-by-status, last-7-days, and
 * duplicate-email totals from /api/dev/reconciliation, shown in the website so the
 * runbook's reconciliation step is visible during the walkthrough. Renders nothing
 * unless the shell set window.__reconciliationTools (absent in production, and no
 * mount-time request in the test harness when the flag is off).
 */
export function ReconciliationPanel({ reloadToken }: ReconciliationPanelProps) {
  const enabled = reconciliationToolsEnabled()
  const [report, setReport] = useState<ReconciliationReport | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!enabled) {
      return
    }
    const controller = new AbortController()
    setLoading(true)
    void (async () => {
      const loaded = await fetchReconciliation(controller.signal)
      if (loaded) {
        setReport(loaded)
        setStatus('')
      } else {
        setStatus('Reconciliation report unavailable.')
      }
      setLoading(false)
    })()
    return () => {
      controller.abort()
    }
  }, [enabled, reloadToken])

  if (!enabled) {
    return null
  }

  return (
    <aside className="dev-recon" aria-label="Developer reconciliation report">
      <div className="dev-recon-heading">
        <span className="dev-tools-tag">DEV</span>
        <strong>Reconciliation</strong>
        <span className="dev-recon-summary" role="status" aria-live="polite">
          {report
            ? `${report.totalCount} stored · ${report.last7DaysCount} in the last 7 days`
            : status || 'Loading…'}
          {loading && <Spinner size={14} className="dev-recon-spinner" />}
        </span>
      </div>

      {report && (
        <>
          <ul className="dev-recon-counts">
            {report.countByStatus.map((entry) => (
              <li key={entry.status}>
                <span className="dev-recon-status">{entry.status}</span>
                <span className="dev-recon-count">{entry.count}</span>
              </li>
            ))}
          </ul>

          {report.duplicateEmailGroups.length > 0 && (
            <p className="dev-recon-duplicates">
              Duplicate emails:{' '}
              {report.duplicateEmailGroups
                .map((group) => `${group.normalizedEmail} ×${group.occurrenceCount}`)
                .join(', ')}
            </p>
          )}
        </>
      )}
    </aside>
  )
}
