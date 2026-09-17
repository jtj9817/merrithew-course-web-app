// Canonical wire shapes (contracts.md C1–C4), transcribed verbatim from
// docs/testing/frontend-and-sql-cases.md §4. Builders return deep-fresh copies.
// Shared types live in src/lib/types.ts so production code never imports tests.

import type {
  Inquiry,
  InquiryPageEnvelope,
  ProblemDetailsShape,
  StatusName,
} from '../lib/types'

export type { Inquiry, InquiryPageEnvelope, ProblemDetailsShape, StatusName }
export { STATUS_NAMES } from '../lib/types'

export function fixInq1(): Inquiry {
  return {
    id: 1,
    firstName: 'Avery',
    lastName: "O'Neill",
    email: 'avery.oneill@example.com',
    phone: '+1 555 0100',
    courseName: 'Yoga Teacher Training — Fall Cohort',
    preferredLocation: 'Calgary NW',
    message: 'Please send the syllabus & pricing.',
    status: 'New',
    createdDate: '2026-09-10T14:00:00Z',
    updatedDate: '2026-09-10T14:00:00Z',
  }
}

export function fixInq2(): Inquiry {
  return {
    id: 2,
    firstName: 'Céline',
    lastName: 'Fernández',
    email: 'celine.fernandez@example.com',
    phone: null,
    courseName: 'Barre Fundamentals',
    preferredLocation: null,
    message: null,
    status: 'Contacted',
    createdDate: '2026-09-09T09:30:00Z',
    updatedDate: '2026-09-12T16:45:00Z',
  }
}

export function fixInqXss(): Inquiry {
  const base = fixInq1()
  return {
    ...base,
    firstName: 'Ada <img src=x onerror="window.__xss=true">',
    message: '<script>window.__xss=true</script> & "quotes" \'apostrophes\'',
  }
}

/** FIX-INQ-1 after a successful PUT to Contacted (C2/C7). */
export function fixInq1Contacted(): Inquiry {
  const base = fixInq1()
  return {
    ...base,
    status: 'Contacted',
    updatedDate: '2026-09-14T10:15:00Z',
  }
}

/** Synthetic multi-row seed: `count` rows starting at `fromId` with createdDate ascending by id. */
export function makeRows(
  count: number,
  status: StatusName = 'New',
  fromId = 1,
): Inquiry[] {
  const rows: Inquiry[] = []
  for (let offset = 0; offset < count; offset += 1) {
    const id = fromId + offset
    const created = new Date(Date.UTC(2026, 8, 1) + id * 60_000)
      .toISOString()
      .replace('.000Z', 'Z')
    rows.push({
      ...fixInq1(),
      id,
      firstName: `First${id}`,
      lastName: `Last${id}`,
      email: `person${id}@example.com`,
      status,
      createdDate: created,
      updatedDate: created,
    })
  }
  return rows
}

export function envelope(
  items: Inquiry[],
  overrides: Partial<Omit<InquiryPageEnvelope, 'items'>> = {},
): InquiryPageEnvelope {
  return {
    items,
    page: overrides.page ?? 1,
    pageSize: overrides.pageSize ?? 20,
    totalCount: overrides.totalCount ?? items.length,
  }
}

export function problem400Validation(): ProblemDetailsShape {
  return {
    type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
    title: 'One or more validation errors occurred.',
    status: 400,
    errors: { status: ['The status field is required.'] },
  }
}

export function problem404(): ProblemDetailsShape {
  return {
    type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
    title: 'Not Found',
    status: 404,
  }
}

export function problem500(): ProblemDetailsShape {
  return {
    title: 'An unexpected error occurred.',
    status: 500,
  }
}

/** Non-canonical 500 carrying unsafe sentinel fields (IT-UI-026). */
export function problem500Dirty(): ProblemDetailsShape {
  return {
    ...problem500(),
    detail: 'SELECT * FROM CourseInquiries',
    exception: 'Server=tcp:test',
  }
}
