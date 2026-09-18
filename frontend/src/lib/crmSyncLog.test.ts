import { describe, expect, it } from 'vitest'
import { buildCrmSyncLog } from './crmSimulation'
import type { CrmSyncResult } from './crmSimulation'

function result(overrides: Partial<CrmSyncResult>): CrmSyncResult {
  return {
    inquiryId: 42,
    mode: 'Success',
    outcome: 'Success',
    attempts: 1,
    ...overrides,
  }
}

describe('buildCrmSyncLog', () => {
  it('reconstructs a single successful attempt', () => {
    const log = buildCrmSyncLog(result({ inquiryId: 42, mode: 'Success', outcome: 'Success', attempts: 1 }))
    expect(log.map((entry) => entry.message)).toEqual([
      'CRM sync attempt 1 for inquiry 42 started',
      'CRM sync attempt 1 for inquiry 42 ended with outcome success',
      'CRM sync for inquiry 42 ended with outcome success after 1 attempt(s)',
    ])
    expect(log.every((entry) => entry.level === 'info')).toBe(true)
    expect(log.map((entry) => entry.eventId)).toEqual([10, 11, 13])
  })

  it('reconstructs transient failures that resolve to success', () => {
    const log = buildCrmSyncLog(
      result({ inquiryId: 5, mode: 'TransientThenSuccess', outcome: 'Success', attempts: 3 }),
    )
    expect(log.map((entry) => entry.message)).toEqual([
      'CRM sync attempt 1 for inquiry 5 started',
      'CRM sync attempt 1 for inquiry 5 ended with outcome failed (HttpRequestException)',
      'CRM sync attempt 2 for inquiry 5 started',
      'CRM sync attempt 2 for inquiry 5 ended with outcome failed (HttpRequestException)',
      'CRM sync attempt 3 for inquiry 5 started',
      'CRM sync attempt 3 for inquiry 5 ended with outcome success',
      'CRM sync for inquiry 5 ended with outcome success after 3 attempt(s)',
    ])
    // The retried failures are warnings; the rest are info.
    expect(log.filter((entry) => entry.level === 'warning')).toHaveLength(2)
  })

  it('marks an exhausted transient failure as an HttpRequestException', () => {
    const log = buildCrmSyncLog(
      result({ inquiryId: 9, mode: 'AlwaysTransientFailure', outcome: 'Failed', attempts: 4 }),
    )
    const terminal = log.at(-1)!
    expect(terminal.message).toBe(
      'CRM sync for inquiry 9 ended with outcome failed (HttpRequestException) after 4 attempt(s)',
    )
    expect(terminal.level).toBe('warning')
    expect(terminal.eventId).toBe(14)
  })

  it('marks a permanent rejection as an InvalidOperationException and does not retry', () => {
    const log = buildCrmSyncLog(
      result({ inquiryId: 3, mode: 'PermanentFailure', outcome: 'Failed', attempts: 1 }),
    )
    expect(log.map((entry) => entry.message)).toEqual([
      'CRM sync attempt 1 for inquiry 3 started',
      'CRM sync attempt 1 for inquiry 3 ended with outcome failed (InvalidOperationException)',
      'CRM sync for inquiry 3 ended with outcome failed (InvalidOperationException) after 1 attempt(s)',
    ])
  })

  it('reconstructs repeated timeouts with no error type', () => {
    const log = buildCrmSyncLog(
      result({ inquiryId: 7, mode: 'Timeout', outcome: 'TimedOut', attempts: 3 }),
    )
    expect(log.every((entry) => !entry.message.includes('Exception'))).toBe(true)
    expect(log.at(-1)!.message).toBe(
      'CRM sync for inquiry 7 ended with outcome timedOut after 3 attempt(s)',
    )
    expect(log.every((entry) => entry.level === 'info')).toBe(true)
  })

  it('reconstructs a CRM-side cancellation', () => {
    const log = buildCrmSyncLog(
      result({ inquiryId: 8, mode: 'InternalCancellation', outcome: 'Cancelled', attempts: 1 }),
    )
    expect(log.map((entry) => entry.message)).toEqual([
      'CRM sync attempt 1 for inquiry 8 started',
      'CRM sync attempt 1 for inquiry 8 ended with outcome cancelled',
      'CRM sync for inquiry 8 ended with outcome cancelled after 1 attempt(s)',
    ])
  })
})
