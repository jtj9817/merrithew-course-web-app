// Dev-only client for the read-only reconciliation reports (/api/dev/reconciliation).
// The Razor shell signals that the endpoints are mapped by setting
// window.__reconciliationTools; every response is shape-checked before use.

export interface StatusCount {
  status: string
  count: number
}

export interface DuplicateEmailGroup {
  normalizedEmail: string
  occurrenceCount: number
}

export interface ReconciliationReport {
  countByStatus: StatusCount[]
  totalCount: number
  last7DaysCount: number
  duplicateEmailGroups: DuplicateEmailGroup[]
}

const BASE = '/api/dev/reconciliation'

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isNonNegativeInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0
}

function parseStatusCount(value: unknown): StatusCount | null {
  if (!isObject(value) || typeof value.status !== 'string' || !isNonNegativeInteger(value.count)) {
    return null
  }
  return { status: value.status, count: value.count }
}

function parseDuplicateGroup(value: unknown): DuplicateEmailGroup | null {
  if (!isObject(value) || typeof value.normalizedEmail !== 'string' || !isNonNegativeInteger(value.occurrenceCount)) {
    return null
  }
  return { normalizedEmail: value.normalizedEmail, occurrenceCount: value.occurrenceCount }
}

function parseReport(value: unknown): ReconciliationReport | null {
  if (
    !isObject(value) ||
    !Array.isArray(value.countByStatus) ||
    !Array.isArray(value.duplicateEmailGroups) ||
    !isNonNegativeInteger(value.totalCount) ||
    !isNonNegativeInteger(value.last7DaysCount)
  ) {
    return null
  }

  const countByStatus: StatusCount[] = []
  for (const candidate of value.countByStatus) {
    const parsed = parseStatusCount(candidate)
    if (!parsed) {
      return null
    }
    countByStatus.push(parsed)
  }

  const duplicateEmailGroups: DuplicateEmailGroup[] = []
  for (const candidate of value.duplicateEmailGroups) {
    const parsed = parseDuplicateGroup(candidate)
    if (!parsed) {
      return null
    }
    duplicateEmailGroups.push(parsed)
  }

  return {
    countByStatus,
    totalCount: value.totalCount,
    last7DaysCount: value.last7DaysCount,
    duplicateEmailGroups,
  }
}

/** True when the server shell enabled the reconciliation tools for this page. */
export function reconciliationToolsEnabled(): boolean {
  return window.__reconciliationTools === true
}

/** The reconciliation report over the live store, or null when unavailable. */
export async function fetchReconciliation(signal?: AbortSignal): Promise<ReconciliationReport | null> {
  try {
    const response = await fetch(BASE, { signal, headers: { Accept: 'application/json' } })
    if (!response.ok) {
      return null
    }
    return parseReport(await response.json())
  } catch {
    return null
  }
}
