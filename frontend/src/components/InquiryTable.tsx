import { useEffect, useState } from 'react'
import { formatIsoDate, displayName } from '../lib/format'
import { STATUS_NAMES } from '../lib/types'
import type { Inquiry, StatusName } from '../lib/types'
import type { SortDirection } from '../lib/listQuery'
import { StatusBadge } from './StatusBadge'
interface InquiryTableProps {
  items: Inquiry[]
  fetching: boolean
  mutatingIds: readonly number[]
  failedIds?: readonly number[]
  sort?: SortDirection
  colorblind?: boolean
  onOpenDetail: (id: number, opener: HTMLElement) => void
  onApplyStatus: (id: number, next: StatusName) => void
}

export function InquiryTable({
  items,
  fetching,
  mutatingIds,
  failedIds = [],
  sort,
  colorblind = false,
  onOpenDetail,
  onApplyStatus,
}: InquiryTableProps) {
  return (
    <div className="table-wrap">
      <table
        className="inquiry-table"
        aria-busy={fetching || undefined}
      >
        <caption className="sr-only">Incoming Course Inquiries Queue</caption>
        <thead>
          <tr>
            <th scope="col" className="th-details">Details</th>
            <th scope="col" className="th-name">First name</th>
            <th scope="col" className="th-name">Last name</th>
            <th scope="col" className="th-course">Course</th>
            <th scope="col" className="th-status">Status</th>
            <th
              scope="col"
              className="th-created"
              aria-sort={sort === 'createdDateAsc' ? 'ascending' : 'descending'}
            >
              Created
            </th>
          </tr>
        </thead>
        <tbody>
          {items.map((inquiry) => (
            <InquiryRow
              key={inquiry.id}
              inquiry={inquiry}
              mutating={mutatingIds.includes(inquiry.id)}
              isFailed={failedIds.includes(inquiry.id)}
              colorblind={colorblind}
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
  isFailed?: boolean
  colorblind?: boolean
  onOpenDetail: (id: number, opener: HTMLElement) => void
  onApplyStatus: (id: number, next: StatusName) => void
}

function InquiryRow({
  inquiry,
  mutating,
  isFailed = false,
  colorblind,
  onOpenDetail,
  onApplyStatus,
}: InquiryRowProps) {
  const [draft, setDraft] = useState<StatusName>(inquiry.status)
  const name = displayName(inquiry)

  // Follow persisted changes (e.g. a refresh brought a new status) without
  // clobbering an in-progress selection while a mutation is pending.
  useEffect(() => {
    if (!mutating) {
      setDraft(inquiry.status)
    }
  }, [inquiry.status, mutating])

  const isModified = draft !== inquiry.status

  return (
    <tr>
      <td className="cell-details">
        <button
          type="button"
          className="opener"
          aria-label={`Details for ${name}`}
          onClick={(event) => onOpenDetail(inquiry.id, event.currentTarget)}
        >
          Details
        </button>
      </td>
      <td className="cell-name">{inquiry.firstName}</td>
      <td className="cell-name">{inquiry.lastName}</td>
      <td className="cell-course">{inquiry.courseName}</td>
      <td className="cell-status">
        <div className="status-cell">
          <StatusBadge status={inquiry.status} colorblind={colorblind} />
          <select
            aria-label={`Status for ${name}`}
            aria-invalid={isFailed ? 'true' : undefined}
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
            className={`apply${isModified ? ' apply--modified' : ''}`}
            data-modified={isModified ? 'true' : 'false'}
            aria-label={`Apply for ${name}`}
            disabled={mutating}
            onClick={() => onApplyStatus(inquiry.id, draft)}
          >
            Apply
          </button>
        </div>
      </td>
      <td className="cell-created date-cell">{formatIsoDate(inquiry.createdDate)}</td>
    </tr>
  )
}
