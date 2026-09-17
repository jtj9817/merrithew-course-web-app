import { describe, expect, it } from 'vitest'
import { computeLastPage } from './paging'

describe('UT-UI-004 last-available-page calculator', () => {
  it.each([
    // (3, 41, 20): 41 items at 20/page span 3 pages — the catalog's example
    // value of 2 contradicted its own normative ceil formula; corrected in
    // sync with docs/testing/frontend-and-sql-cases.md.
    { page: 3, totalCount: 41, pageSize: 20, expected: 3 },
    { page: 2, totalCount: 0, pageSize: 20, expected: 1 },
    { page: 1, totalCount: 0, pageSize: 20, expected: 1 },
    { page: 3, totalCount: 40, pageSize: 20, expected: 2 },
  ])(
    'computeLastPage(page=$page, totalCount=$totalCount, pageSize=$pageSize) → $expected',
    ({ page, totalCount, pageSize, expected }) => {
      expect(computeLastPage(page, totalCount, pageSize)).toBe(expected)
    },
  )

  it('never returns 0 and never exceeds the last page', () => {
    expect(computeLastPage(1, 0, 20)).toBeGreaterThanOrEqual(1)
    expect(computeLastPage(1, 19, 20)).toBe(1)
    expect(computeLastPage(1, 21, 20)).toBe(2)
  })
})
