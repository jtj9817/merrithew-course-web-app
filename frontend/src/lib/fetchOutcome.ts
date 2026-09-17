import type { Inquiry, InquiryPageEnvelope, ProblemDetailsShape } from './types'

export type ApiOutcome =
  | { kind: 'data'; envelope: InquiryPageEnvelope }
  | { kind: 'record'; inquiry: Inquiry }
  | { kind: 'validation'; fields: string[] }
  | { kind: 'gone' }
  | { kind: 'problem'; title: string }
  | { kind: 'generic' }

export type FetchOutcome = Extract<
  ApiOutcome,
  { kind: 'data' | 'validation' | 'gone' | 'problem' | 'generic' }
>

export type RecordOutcome = Extract<
  ApiOutcome,
  { kind: 'record' | 'validation' | 'gone' | 'problem' | 'generic' }
>

/**
 * Classifies a list response (C3/C7): envelopes surface as data; validation
 * failures surface field keys only (never values); 404s read as "gone";
 * anything unparseable or non-JSON degrades to the generic recoverable
 * message rather than a parsing crash.
 */
export async function classifyResponse(response: Response): Promise<FetchOutcome> {
  if (response.status === 404) {
    return { kind: 'gone' }
  }

  const body = await readJsonBody(response)
  if (body === null) {
    return { kind: 'generic' }
  }

  if (response.ok) {
    return isEnvelope(body) ? { kind: 'data', envelope: body } : { kind: 'generic' }
  }

  return problemOutcome(response.status, body)
}

/** Classifies a detail/status-update response against the single-record shape. */
export async function classifyRecordResponse(response: Response): Promise<RecordOutcome> {
  if (response.status === 404) {
    return { kind: 'gone' }
  }

  const body = await readJsonBody(response)
  if (body === null) {
    return { kind: 'generic' }
  }

  if (response.ok) {
    return isInquiry(body) ? { kind: 'record', inquiry: body } : { kind: 'generic' }
  }

  return problemOutcome(response.status, body)
}

/**
 * Network-level failures and thrown errors get the generic recoverable path.
 * The narrow return type is assignable to both {@link FetchOutcome} and
 * {@link RecordOutcome}, so every transport catch can route through here.
 */
export function classifyFailure(_error: unknown): Extract<ApiOutcome, { kind: 'generic' }> {
  return { kind: 'generic' }
}

export const OUTCOME_MESSAGES = {
  listFailure: 'Inquiries could not be loaded — something went wrong. Select Retry to try again.',
  mutationFailure: 'The status could not be saved — something went wrong. Try again.',
  saved: 'Status saved.',
  savedRefreshFailed:
    'Status saved, but the list could not be refreshed — it may be out of date.',
  gone: 'This inquiry no longer exists.',
} as const

/** Fixed message variant for a classified outcome (C7). */
export function messageFor(outcome: ApiOutcome): string {
  switch (outcome.kind) {
    case 'data':
    case 'record':
      return OUTCOME_MESSAGES.saved
    case 'validation':
      return `The update was rejected — invalid field: ${outcome.fields.join(', ')}. Choose a status from the list and try again.`
    case 'gone':
      return OUTCOME_MESSAGES.gone
    case 'problem':
      return outcome.title
    case 'generic':
      return OUTCOME_MESSAGES.listFailure
  }
}

async function readJsonBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return null
  }

  try {
    return await response.json()
  } catch {
    return null
  }
}

type SharedOutcome = Extract<ApiOutcome, { kind: 'validation' | 'problem' | 'generic' }>

function problemOutcome(status: number, body: unknown): SharedOutcome {
  const problem = body as ProblemDetailsShape
  if (status === 400 && problem?.errors && typeof problem.errors === 'object') {
    const fields = Object.keys(problem.errors)
    if (fields.length > 0) {
      return { kind: 'validation', fields }
    }
  }

  const title = typeof problem?.title === 'string' && problem.title.trim() !== ''
    ? problem.title
    : ''
  return title ? { kind: 'problem', title } : { kind: 'generic' }
}

function isEnvelope(value: unknown): value is InquiryPageEnvelope {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const candidate = value as Record<string, unknown>
  return Array.isArray(candidate.items) && typeof candidate.page === 'number'
    && typeof candidate.pageSize === 'number' && typeof candidate.totalCount === 'number'
}

function isInquiry(value: unknown): value is Inquiry {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const candidate = value as Record<string, unknown>
  return typeof candidate.id === 'number' && typeof candidate.firstName === 'string'
    && typeof candidate.lastName === 'string' && typeof candidate.email === 'string'
    && typeof candidate.courseName === 'string' && typeof candidate.status === 'string'
    && typeof candidate.createdDate === 'string' && typeof candidate.updatedDate === 'string'
}
