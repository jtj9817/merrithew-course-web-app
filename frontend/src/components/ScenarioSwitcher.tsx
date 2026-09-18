import { useEffect, useRef, useState } from 'react'
import {
  applyScenario,
  clearScenarios,
  fetchScenarios,
  scenarioToolsEnabled,
} from '../lib/scenarios'
import type { ScenarioInfo } from '../lib/scenarios'
import { LinearProgress, Spinner } from './Progress'

interface ScenarioSwitcherProps {
  /** Called after the store changes so the dashboard reloads its data. */
  onChanged: () => void
  /** Routes the outcome to the shared triage snackbar. */
  onNotify: (message: string, variant: 'info' | 'error') => void
}

type PendingAction = 'apply' | 'clear' | null

/**
 * Dev-only control that drives the inquiry store into a known state through
 * /api/dev/scenarios, with a Clear action to reverse it. It renders nothing
 * unless the server shell set `window.__scenarioTools`, so it is absent in
 * production and inert in the test harness (no mount-time request is made when
 * the flag is off, keeping the fetch double's queue intact).
 *
 * In-flight work shows an M3 linear progress bar plus a spinner in the active
 * button; the result is announced through the shared snackbar.
 */
export function ScenarioSwitcher({ onChanged, onNotify }: ScenarioSwitcherProps) {
  const enabled = scenarioToolsEnabled()
  const [scenarios, setScenarios] = useState<ScenarioInfo[] | null>(null)
  const [selected, setSelected] = useState('')
  const [pending, setPending] = useState<PendingAction>(null)

  const onChangedRef = useRef(onChanged)
  onChangedRef.current = onChanged
  const onNotifyRef = useRef(onNotify)
  onNotifyRef.current = onNotify

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

  const busy = pending !== null

  const apply = async () => {
    setPending('apply')
    const result = await applyScenario(selected)
    if (result) {
      onChangedRef.current()
      onNotifyRef.current(`Loaded ${result.scenario}: ${result.totalCount} inquiries.`, 'info')
    } else {
      onNotifyRef.current('Could not load the scenario. Is scenario seeding still enabled?', 'error')
    }
    setPending(null)
  }

  const clear = async () => {
    setPending('clear')
    const removed = await clearScenarios()
    if (removed === null) {
      onNotifyRef.current('Could not clear the store. Is scenario seeding still enabled?', 'error')
    } else {
      onChangedRef.current()
      onNotifyRef.current(`Cleared ${removed} ${removed === 1 ? 'inquiry' : 'inquiries'}.`, 'info')
    }
    setPending(null)
  }

  return (
    <aside className="dev-scenarios" aria-label="Developer scenario tools" aria-busy={busy}>
      {busy && (
        <div className="dev-tool-progress">
          <LinearProgress label={pending === 'apply' ? 'Loading scenario' : 'Clearing inquiries'} />
        </div>
      )}
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
      <button type="button" onClick={() => void apply()} disabled={busy}>
        {pending === 'apply' && <Spinner size={16} />}
        Apply
      </button>
      <button type="button" onClick={() => void clear()} disabled={busy}>
        {pending === 'clear' && <Spinner size={16} />}
        Clear
      </button>
    </aside>
  )
}
