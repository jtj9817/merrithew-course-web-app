// Dev-only client for the scenario-seeding endpoints (/api/dev/scenarios).
// These are served only when the backend enables scenario seeding; the shell
// signals that by setting window.__scenarioTools, which gates the switcher UI.

export interface ScenarioInfo {
  name: string
  description: string
  size: number
}

export interface SeedResult {
  scenario: string
  reset: boolean
  created: number
  statusUpdates: number
  totalCount: number
  totalsByStatus: Record<string, number>
}

const BASE = '/api/dev/scenarios'

/** True when the server shell enabled the dev scenario tools for this page. */
export function scenarioToolsEnabled(): boolean {
  return window.__scenarioTools === true
}

/** Lists the available scenarios, or null when the endpoint is unavailable. */
export async function fetchScenarios(signal?: AbortSignal): Promise<ScenarioInfo[] | null> {
  try {
    const response = await fetch(BASE, { signal, headers: { Accept: 'application/json' } })
    if (!response.ok) {
      return null
    }
    return (await response.json()) as ScenarioInfo[]
  } catch {
    return null
  }
}

/** Seeds a scenario (resetting the store first); null on failure. */
export async function applyScenario(name: string): Promise<SeedResult | null> {
  try {
    const response = await fetch(`${BASE}/${encodeURIComponent(name)}`, {
      method: 'POST',
      headers: { Accept: 'application/json' },
    })
    if (!response.ok) {
      return null
    }
    return (await response.json()) as SeedResult
  } catch {
    return null
  }
}

/** Clears every inquiry (the reverse of seeding); returns the count removed, or null on failure. */
export async function clearScenarios(): Promise<number | null> {
  try {
    const response = await fetch(BASE, { method: 'DELETE', headers: { Accept: 'application/json' } })
    if (!response.ok) {
      return null
    }
    const body = (await response.json()) as { removed: number }
    return body.removed
  } catch {
    return null
  }
}
