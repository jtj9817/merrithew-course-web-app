import type { FetchOutcome, RecordOutcome } from './fetchOutcome'
import { classifyFailure, classifyRecordResponse, classifyResponse } from './fetchOutcome'
import { serializeListQuery } from './listQuery'
import type { ListQueryState } from './listQuery'
import type { StatusName } from './types'

/** Same-origin GET of a filtered/paged inquiry list (C4 wire shape). */
export async function fetchInquiryPage(
  query: ListQueryState,
  signal?: AbortSignal,
): Promise<FetchOutcome> {
  try {
    const response = await fetch(serializeListQuery(query), {
      signal,
      headers: { Accept: 'application/json' },
    })
    return await classifyResponse(response)
  } catch (error) {
    return classifyFailure(error)
  }
}

/** Same-origin GET of one inquiry for the detail panel. */
export async function fetchInquiry(id: number, signal?: AbortSignal): Promise<RecordOutcome> {
  try {
    const response = await fetch(`/api/inquiries/${id}`, {
      signal,
      headers: { Accept: 'application/json' },
    })
    return await classifyRecordResponse(response)
  } catch (error) {
    return classifyFailure(error)
  }
}

/** Status update: the body is exactly the single canonical status field (C2/C7). */
export async function putInquiryStatus(id: number, status: StatusName): Promise<RecordOutcome> {
  try {
    const response = await fetch(`/api/inquiries/${id}/status`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ status }),
    })
    return await classifyRecordResponse(response)
  } catch (error) {
    return classifyFailure(error)
  }
}
