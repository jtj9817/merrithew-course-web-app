import { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { buildCrmSyncLog } from '../lib/crmSimulation'
import type { CrmSimulationSettings, CrmSyncResult } from '../lib/crmSimulation'

/** A completed run: the recorded result plus the settings snapshot it used. */
export interface CrmSyncRun {
  result: CrmSyncResult
  settings: CrmSimulationSettings
}

interface CrmSyncLogModalProps {
  /** The run to show, or null when the modal is closed. */
  run: CrmSyncRun | null
  onClose: () => void
}

const OUTCOME_LABELS: Record<CrmSyncResult['outcome'], string> = {
  Success: 'Succeeded',
  Failed: 'Failed',
  TimedOut: 'Timed out',
  Cancelled: 'Cancelled',
}

/**
 * Dev-only modal that shows the reconstructed CRM sync trail for one demo run —
 * the one dev control whose outcome is a multi-attempt sequence worth reading
 * line by line. Built on the same M3 dialog conventions as the detail panel:
 * role="dialog", aria-modal, focus moves in on open and returns to the opener on
 * close, Escape closes, the background scrolls locked, and Tab is trapped inside.
 */
export function CrmSyncLogModal({ run, onClose }: CrmSyncLogModalProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const openerRef = useRef<HTMLElement | null>(null)
  const isOpen = run !== null

  useEffect(() => {
    if (!isOpen) {
      return
    }
    openerRef.current = document.activeElement as HTMLElement | null
    containerRef.current?.focus()
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
      const opener = openerRef.current
      if (opener?.isConnected) {
        opener.focus()
      }
    }
  }, [isOpen])

  if (!run) {
    return null
  }

  const { result, settings } = run
  const entries = buildCrmSyncLog(result)

  const handleKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault()
      event.stopPropagation()
      onClose()
      return
    }

    if (event.key === 'Tab') {
      const container = containerRef.current
      if (!container) {
        return
      }
      const focusable = container.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )
      if (focusable.length === 0) {
        event.preventDefault()
        return
      }
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey) {
        if (document.activeElement === first || document.activeElement === container) {
          event.preventDefault()
          last.focus()
        }
      } else if (document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
  }

  const summary: Array<{ term: string; value: string }> = [
    { term: 'Inquiry', value: `#${result.inquiryId}` },
    { term: 'Behavior', value: result.mode },
    { term: 'Outcome', value: OUTCOME_LABELS[result.outcome] },
    { term: 'Attempts', value: String(result.attempts) },
    { term: 'Latency', value: `${settings.latencyMilliseconds} ms` },
  ]
  if (result.mode === 'TransientThenSuccess') {
    summary.push({ term: 'Failures first', value: String(settings.transientFailuresBeforeSuccess) })
  }

  return createPortal(
    <div
      className="dev-log-scrim"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
    >
      <div
        ref={containerRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby="crm-log-title"
        aria-describedby="crm-log-note"
        className="dev-log-dialog"
        onKeyDown={handleKeyDown}
      >
        <div className="dev-log-head">
          <div>
            <p className="dev-log-eyebrow">CRM simulation</p>
            <h2 id="crm-log-title" className="dev-log-title">
              Sync log · inquiry #{result.inquiryId}
            </h2>
          </div>
          <button type="button" className="dev-log-close" onClick={onClose}>
            Close
          </button>
        </div>

        <dl className="dev-log-summary">
          {summary.map((item) => (
            <div key={item.term} className="dev-log-summary-item">
              <dt>{item.term}</dt>
              <dd>{item.value}</dd>
            </div>
          ))}
        </dl>

        <ol className="dev-log-lines">
          {entries.map((entry, index) => (
            <li
              key={index}
              className={`dev-log-line dev-log-line--${entry.level}`}
            >
              <span className="dev-log-level" aria-hidden="true">
                {entry.level === 'warning' ? 'WARN' : 'INFO'}
              </span>
              <span className="dev-log-event" aria-hidden="true">
                {entry.eventId}
              </span>
              <code className="dev-log-message">{entry.message}</code>
            </li>
          ))}
        </ol>

        <p id="crm-log-note" className="dev-log-note">
          Reconstructed from the recorded sync result to mirror the server-side log. The
          application log remains the authoritative record.
        </p>
      </div>
    </div>,
    document.body,
  )
}
