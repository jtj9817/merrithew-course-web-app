import { useEffect, useState } from 'react'
import { formatIsoDate, displayName } from '../lib/format'
import { STATUS_NAMES } from '../lib/types'
import type { Inquiry, StatusName } from '../lib/types'

interface InquiryTableProps {
  items: Inquiry[]
  fetching: boolean
  mutatingIds: readonly number[]
  onOpenDetail: (id: number, opener: HTMLElement) => void
  onApplyStatus: (id: number, next: StatusName) => void
}

export function InquiryTable({
  items,
  fetching,
  mutatingIds,
  onOpenDetail,
  onApplyStatus,
}: InquiryTableProps) {
  return (
    <div className="table-wrap">
      <table className="inquiry-table" aria-busy={fetching || undefined}>
        <thead>
          <tr>
            <th scope="col">Details</th>
            <th scope="col">First name</th>
            <th scope="col">Last name</th>
            <th scope="col">Course</th>
            <th scope="col">Status</th>
            <th scope="col">Created</th>
          </tr>
        </thead>
        <tbody>
          {items.map((inquiry) => (
            <InquiryRow
              key={inquiry.id}
              inquiry={inquiry}
              mutating={mutatingIds.includes(inquiry.id)}
              onOpenDetail={onOpenDetail}
              onApplyStatus={onApplyStatus}
            />
          ))}
        </tbody>
      </table>
    </div>
  )
}

interface InquiryRowProps {
  inquiry: Inquiry
  mutating: boolean
  onOpenDetail: (id: number, opener: HTMLElement) => void
  onApplyStatus: (id: number, next: StatusName) => void
}

function InquiryRow({ inquiry, mutating, onOpenDetail, onApplyStatus }: InquiryRowProps) {
  const [draft, setDraft] = useState<StatusName>(inquiry.status)
  const name = displayName(inquiry)

  // Follow persisted changes (e.g. a refresh brought a new status) without
  // clobbering an in-progress selection while a mutation is pending.
  useEffect(() => {
    if (!mutating) {
      setDraft(inquiry.status)
    }
  }, [inquiry.status, mutating])

  return (
    <tr>
      <td>
        <button
          type="button"
          className="opener"
          aria-label={`Details for ${name}`}
          onClick={(event) => onOpenDetail(inquiry.id, event.currentTarget)}
        >
          Details
        </button>
      </td>
      <td>{inquiry.firstName}</td>
      <td>{inquiry.lastName}</td>
      <td>{inquiry.courseName}</td>
      <td>
        <div className="status-cell">
          <span className={`status-badge status-${inquiry.status.toLowerCase()}`}>
            {inquiry.status}
          </span>
          <select
            aria-label={`Status for ${name}`}
            name={`inquiry-${inquiry.id}-status`}
            value={draft}
            onChange={(event) => setDraft(event.target.value as StatusName)}
          >
            {STATUS_NAMES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="apply"
            aria-label={`Apply for ${name}`}
            disabled={mutating}
            onClick={() => onApplyStatus(inquiry.id, draft)}
          >
            Apply
          </button>
        </div>
      </td>
      <td className="date-cell">{formatIsoDate(inquiry.createdDate)}</td>
    </tr>
  )
}
