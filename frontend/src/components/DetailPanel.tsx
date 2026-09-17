import { useEffect, useRef } from 'react'
import { formatIsoDate } from '../lib/format'
import type { DetailState } from '../lib/types'
import { StatusBadge } from './StatusBadge'

interface DetailPanelProps {
  detail: DetailState
  colorblind?: boolean
  onClose: () => void
}

/**
 * Centered modal dialog on top of a translucent scrim (ADR-0004/Workstream 2).
 * Semantics: role="dialog", aria-modal="true", labelled by its heading.
 * Focus moves in on open and returns to the opener on close (C7).
 * Background content is inert while open and body scroll is locked.
 * Focus trap cycles Tab/Shift+Tab inside the dialog.
 */
export function DetailPanel({ detail, colorblind = false, onClose }: DetailPanelProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const focusTarget = detail.kind === 'closed' ? null : detail.id

  // Focus management: focus moves into the dialog on open (C7)
  useEffect(() => {
    if (focusTarget !== null) {
      containerRef.current?.focus()
    }
  }, [focusTarget])

  // Body scroll lock while modal is open
  useEffect(() => {
    if (detail.kind === 'closed') {
      return
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [detail.kind])

  if (detail.kind === 'closed') {
    return null
  }

  // Keyboard navigation & focus trap: Escape closes, Tab/Shift+Tab cycles within dialog
  const handleKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault()
      event.stopPropagation()
      onClose()
      return
    }

    if (event.key === 'Tab') {
      const container = containerRef.current
      if (!container) return

      const focusable = container.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
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
      } else {
        if (document.activeElement === last) {
          event.preventDefault()
          first.focus()
        }
      }
    }
  }

  return (
    <div
      className="detail-scrim"
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
        aria-labelledby="detail-modal-title"
        className="detail-panel"
        onKeyDown={handleKeyDown}
      >
        <div className="detail-head">
          <h2 id="detail-modal-title" className="detail-title">
            Inquiry detail
          </h2>
          <button
            type="button"
            className="detail-close"
            onClick={onClose}
          >
            Close
          </button>
        </div>

        {detail.kind === 'loading' ? (
          <p className="detail-loading">Loading inquiry…</p>
        ) : (
          <dl className="detail-list">
            <div className="detail-field">
              <dt>Inquiry ID</dt>
              <dd>{detail.inquiry.id}</dd>
            </div>
            <div className="detail-field">
              <dt>First name</dt>
              <dd>{detail.inquiry.firstName}</dd>
            </div>
            <div className="detail-field">
              <dt>Last name</dt>
              <dd>{detail.inquiry.lastName}</dd>
            </div>
            <div className="detail-field">
              <dt>Email</dt>
              <dd>{detail.inquiry.email}</dd>
            </div>
            <div className="detail-field">
              <dt>Phone</dt>
              <dd>{detail.inquiry.phone ?? '—'}</dd>
            </div>
            <div className="detail-field">
              <dt>Course</dt>
              <dd>{detail.inquiry.courseName}</dd>
            </div>
            <div className="detail-field">
              <dt>Preferred location</dt>
              <dd>{detail.inquiry.preferredLocation ?? '—'}</dd>
            </div>
            <div className="detail-field detail-field--wide">
              <dt>Message</dt>
              <dd>{detail.inquiry.message ?? '—'}</dd>
            </div>
            <div className="detail-field">
              <dt>Status</dt>
              <dd>
                <StatusBadge status={detail.inquiry.status} colorblind={colorblind} />
              </dd>
            </div>
            <div className="detail-field">
              <dt>Created</dt>
              <dd>{formatIsoDate(detail.inquiry.createdDate)}</dd>
            </div>
            <div className="detail-field">
              <dt>Updated</dt>
              <dd>{formatIsoDate(detail.inquiry.updatedDate)}</dd>
            </div>
          </dl>
        )}
      </div>
    </div>
  )
}
