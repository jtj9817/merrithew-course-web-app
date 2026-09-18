// Dev-only client for the intake fault-injection controls (/api/dev/intake-fault).
// The Razor shell signals that the endpoints are mapped by setting
// window.__intakeFaultTools; every response is shape-checked before use.

export interface IntakeFaultState {
  /** Creates still queued to fail on their next attempt (0 = inert). */
  armed: number
  /** How many creates the runtime has failed since the process started. */
  totalInjected: number
}

/** Outcome of the demonstration POST /api/inquiries submission. */
export interface IntakeSubmitResult {
  /** The HTTP status the real intake endpoint returned. */
  status: number
  /** The ProblemDetails traceId when the request failed (500/400); null otherwise. */
  traceId: string | null
}

const BASE = '/api/dev/intake-fault'
export const MAX_ARM_COUNT = 100

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isNonNegativeInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0
}

function parseState(value: unknown): IntakeFaultState | null {
  if (!isObject(value) || !isNonNegativeInteger(value.armed) || !isNonNegativeInteger(value.totalInjected)) {
    return null
  }
  return { armed: value.armed, totalInjected: value.totalInjected }
}

/** True when the server shell enabled the intake fault tools for this page. */
export function intakeFaultToolsEnabled(): boolean {
  return window.__intakeFaultTools === true
}

/** Current switch state, or null when the endpoint is unavailable. */
export async function fetchIntakeFault(signal?: AbortSignal): Promise<IntakeFaultState | null> {
  try {
    const response = await fetch(BASE, { signal, headers: { Accept: 'application/json' } })
    if (!response.ok) {
      return null
    }
    return parseState(await response.json())
  } catch {
    return null
  }
}

/** Arms the next `armCount` creates to fail (0 disarms); null on validation/transport failure. */
export async function armIntakeFault(armCount: number): Promise<IntakeFaultState | null> {
  try {
    const response = await fetch(BASE, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ armCount }),
    })
    if (!response.ok) {
      return null
    }
    return parseState(await response.json())
  } catch {
    return null
  }
}

/**
 * Submits one inquiry through the real production endpoint. When a fault is armed the
 * server throws before persisting, so this is expected to return 500 with a traceId
 * and store no row — the genuine "backend/DB failure → never stored" case. Returns
 * the status and (on failure) the ProblemDetails traceId, or null on transport error.
 */
export async function submitFaultDemoInquiry(): Promise<IntakeSubmitResult | null> {
  const stamp = Date.now().toString(36)
  try {
    const response = await fetch('/api/inquiries', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({
        firstName: 'Fault',
        lastName: 'Demo',
        email: `fault.demo.${stamp}@example.test`,
        courseName: 'Intake fault demonstration',
        preferredLocation: 'Toronto',
        message: 'Development-only intake fault demonstration.',
      }),
    })

    let traceId: string | null = null
    try {
      const body: unknown = await response.json()
      if (isObject(body) && typeof body.traceId === 'string') {
        traceId = body.traceId
      }
    } catch {
      // A 201 success body is the created inquiry (no traceId); ignore parse issues.
    }

    return { status: response.status, traceId }
  } catch {
    return null
  }
}
