import { useEffect, useRef } from 'react'
import { formatIsoDate } from '../lib/format'
import type { DetailState } from '../lib/types'

interface DetailPanelProps {
  detail: DetailState
  onClose: () => void
}

/** The single detail drawer: focus moves in on open and returns to the opener on close (C7). */
export function DetailPanel({ detail, onClose }: DetailPanelProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const focusTarget = detail.kind === 'closed' ? null : detail.id

  useEffect(() => {
    if (focusTarget !== null) {
      containerRef.current?.focus()
    }
  }, [focusTarget])

  if (detail.kind === 'closed') {
    return null
  }

  return (
    <div
      ref={containerRef}
      tabIndex={-1}
      className="detail-panel"
      aria-label="Inquiry detail"
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          onClose()
        }
      }}
    >
      <div className="detail-head">
        <h2 className="detail-title">Inquiry detail</h2>
        <button type="button" className="detail-close" onClick={onClose}>
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
              <span className={`status-badge status-${detail.inquiry.status.toLowerCase()}`}>
                {detail.inquiry.status}
              </span>
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
  )
}
