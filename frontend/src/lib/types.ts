/** Wire shapes shared by the island and its test fixtures (contracts.md C1–C4). */

export type StatusName = 'New' | 'Contacted' | 'Pending' | 'Registered' | 'Closed'

export const STATUS_NAMES: readonly StatusName[] = [
  'New',
  'Contacted',
  'Pending',
  'Registered',
  'Closed',
] as const

export interface Inquiry {
  id: number
  firstName: string
  lastName: string
  email: string
  phone: string | null
  courseName: string
  preferredLocation: string | null
  message: string | null
  status: StatusName
  createdDate: string
  updatedDate: string
}

export interface InquiryPageEnvelope {
  items: Inquiry[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ProblemDetailsShape {
  type?: string
  title?: string
  status?: number
  errors?: Record<string, string[]>
  [key: string]: unknown
}

/** Detail-panel view state; the opener is remembered so close can return focus (C7). */
export type DetailState =
  | { kind: 'closed' }
  | { kind: 'loading'; id: number; opener: HTMLElement }
  | { kind: 'open'; id: number; inquiry: Inquiry; opener: HTMLElement }
