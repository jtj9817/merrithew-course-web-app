import { useEffect, useRef, useState } from 'react'
import {
  applyScenario,
  clearScenarios,
  fetchScenarios,
  scenarioToolsEnabled,
} from '../lib/scenarios'
import type { ScenarioInfo } from '../lib/scenarios'

interface ScenarioSwitcherProps {
  /** Called after the store changes so the dashboard reloads its data. */
  onChanged: () => void
}

/**
 * Dev-only control that drives the inquiry store into a known state through
 * /api/dev/scenarios, with a Clear action to reverse it. It renders nothing
 * unless the server shell set `window.__scenarioTools` — so it is absent in
 * production and inert in the test harness (no mount-time request is made when
 * the flag is off, keeping the fetch double's queue intact).
 */
export function ScenarioSwitcher({ onChanged }: ScenarioSwitcherProps) {
  const enabled = scenarioToolsEnabled()
  const [scenarios, setScenarios] = useState<ScenarioInfo[] | null>(null)
  const [selected, setSelected] = useState('')
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')

  const onChangedRef = useRef(onChanged)
  onChangedRef.current = onChanged

  useEffect(() => {
    if (!enabled) {
      return
    }
    const controller = new AbortController()
    void (async () => {
      const list = await fetchScenarios(controller.signal)
      if (list && list.length > 0) {
        setScenarios(list)
        setSelected(list[0].name)
      }
    })()
    return () => controller.abort()
  }, [enabled])

  if (!enabled || !scenarios) {
    return null
  }

  const run = async (action: () => Promise<string | null>) => {
    setBusy(true)
    setStatus('Working…')
    const message = await action()
    if (message) {
      setStatus(message)
      onChangedRef.current()
    } else {
      setStatus('Request failed — is scenario seeding still enabled?')
    }
    setBusy(false)
  }

  const apply = () =>
    run(async () => {
      const result = await applyScenario(selected)
      return result ? `Loaded ${result.scenario} — ${result.totalCount} inquiries.` : null
    })

  const clear = () =>
    run(async () => {
      const removed = await clearScenarios()
      return removed === null ? null : `Cleared ${removed} inquiries.`
    })

  return (
    <aside className="dev-scenarios" aria-label="Developer scenario tools">
      <span className="dev-scenarios-tag">DEV</span>
      <label className="dev-scenarios-field">
        <span>Simulate scenario</span>
        <select
          value={selected}
          disabled={busy}
          onChange={(event) => setSelected(event.target.value)}
        >
          {scenarios.map((scenario) => (
            <option key={scenario.name} value={scenario.name} title={scenario.description}>
              {scenario.name} ({scenario.size})
            </option>
          ))}
        </select>
      </label>
      <button type="button" onClick={apply} disabled={busy}>
        Apply
      </button>
      <button type="button" onClick={clear} disabled={busy}>
        Clear
      </button>
      <span className="dev-scenarios-status" role="status" aria-live="polite">
        {status}
      </span>
    </aside>
  )
}
