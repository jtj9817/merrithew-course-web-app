// Dev-only client for the runtime CRM simulation controls (/api/dev/crm-simulation).
// The Razor shell signals that the endpoints are mapped by setting
// window.__crmSimulationTools; every response is shape-checked before use.

export type CrmSimulationMode =
  | 'Success'
  | 'TransientThenSuccess'
  | 'AlwaysTransientFailure'
  | 'PermanentFailure'
  | 'Timeout'
  | 'InternalCancellation'

export type CrmSyncOutcome = 'Success' | 'Failed' | 'TimedOut' | 'Cancelled'

export interface CrmSimulationSettings {
  mode: CrmSimulationMode
  transientFailuresBeforeSuccess: number
  latencyMilliseconds: number
}

export interface CrmSimulationModeInfo {
  name: CrmSimulationMode
  description: string
}

export interface CrmSimulationCatalog {
  settings: CrmSimulationSettings
  modes: CrmSimulationModeInfo[]
}

export interface CrmSyncResult {
  inquiryId: number
  mode: CrmSimulationMode
  outcome: CrmSyncOutcome
  attempts: number
}

const BASE = '/api/dev/crm-simulation'
const MODES: readonly CrmSimulationMode[] = [
  'Success',
  'TransientThenSuccess',
  'AlwaysTransientFailure',
  'PermanentFailure',
  'Timeout',
  'InternalCancellation',
]
const OUTCOMES: readonly CrmSyncOutcome[] = ['Success', 'Failed', 'TimedOut', 'Cancelled']

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isIntegerInRange(value: unknown, min: number, max: number): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= min && value <= max
}

function isMode(value: unknown): value is CrmSimulationMode {
  return typeof value === 'string' && MODES.some((mode) => mode === value)
}

function isOutcome(value: unknown): value is CrmSyncOutcome {
  return typeof value === 'string' && OUTCOMES.some((outcome) => outcome === value)
}

function parseSettings(value: unknown): CrmSimulationSettings | null {
  if (!isObject(value) || !isMode(value.mode)) {
    return null
  }
  if (
    !isIntegerInRange(value.transientFailuresBeforeSuccess, 0, 3) ||
    !isIntegerInRange(value.latencyMilliseconds, 0, 400)
  ) {
    return null
  }
  return {
    mode: value.mode,
    transientFailuresBeforeSuccess: value.transientFailuresBeforeSuccess,
    latencyMilliseconds: value.latencyMilliseconds,
  }
}

function parseCatalog(value: unknown): CrmSimulationCatalog | null {
  if (!isObject(value) || !Array.isArray(value.modes)) {
    return null
  }
  const settings = parseSettings(value.settings)
  if (!settings) {
    return null
  }

  const modes: CrmSimulationModeInfo[] = []
  for (const candidate of value.modes) {
    if (!isObject(candidate) || !isMode(candidate.name) || typeof candidate.description !== 'string') {
      return null
    }
    modes.push({ name: candidate.name, description: candidate.description })
  }
  return { settings, modes }
}

function parseResult(value: unknown): CrmSyncResult | null {
  if (!isObject(value) || !isMode(value.mode) || !isOutcome(value.outcome)) {
    return null
  }
  const inquiryId = value.inquiryId
  const attempts = value.attempts
  if (typeof inquiryId !== 'number' || !Number.isInteger(inquiryId) || inquiryId <= 0) {
    return null
  }
  if (!isIntegerInRange(attempts, 1, 4)) {
    return null
  }
  return {
    inquiryId,
    mode: value.mode,
    outcome: value.outcome,
    attempts,
  }
}

/** True when the server shell enabled the CRM simulation tools for this page. */
export function crmSimulationToolsEnabled(): boolean {
  return window.__crmSimulationTools === true
}

/** Current settings and the mode catalog, or null when unavailable. */
export async function fetchCrmSimulation(
  signal?: AbortSignal,
): Promise<CrmSimulationCatalog | null> {
  try {
    const response = await fetch(BASE, { signal, headers: { Accept: 'application/json' } })
    if (!response.ok) {
      return null
    }
    return parseCatalog(await response.json())
  } catch {
    return null
  }
}

/** Applies new runtime settings; null on validation or transport failure. */
export async function updateCrmSimulation(
  settings: CrmSimulationSettings,
): Promise<CrmSimulationSettings | null> {
  try {
    const response = await fetch(BASE, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(settings),
    })
    if (!response.ok) {
      return null
    }
    return parseSettings(await response.json())
  } catch {
    return null
  }
}

/**
 * Creates the demonstration inquiry through the real production endpoint, so the
 * exercise covers validation, persistence, the CRM attempt, and isolation exactly
 * as a visitor submission would. Returns the new inquiry id, or null on failure.
 */
export async function createCrmDemoInquiry(): Promise<number | null> {
  const stamp = Date.now().toString(36)
  try {
    const response = await fetch('/api/inquiries', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({
        firstName: 'CRM',
        lastName: 'Demo',
        email: `crm.demo.${stamp}@example.test`,
        phone: '+1 416 555 0199',
        courseName: 'CRM integration demonstration',
        preferredLocation: 'Toronto',
        message: 'Development-only CRM delivery demonstration.',
      }),
    })
    if (response.status !== 201) {
      return null
    }
    const body: unknown = await response.json()
    return isObject(body) && typeof body.id === 'number' ? body.id : null
  } catch {
    return null
  }
}

/** Reads the safe outcome of one completed sync; null when unknown or unavailable. */
export async function fetchCrmSyncResult(inquiryId: number): Promise<CrmSyncResult | null> {
  try {
    const response = await fetch(`${BASE}/results/${inquiryId}`, {
      headers: { Accept: 'application/json' },
    })
    if (!response.ok) {
      return null
    }
    return parseResult(await response.json())
  } catch {
    return null
  }
}
