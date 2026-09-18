import { useEffect, useRef, useState } from 'react'
import {
  createCrmDemoInquiry,
  crmSimulationToolsEnabled,
  fetchCrmSimulation,
  fetchCrmSyncResult,
  updateCrmSimulation,
} from '../lib/crmSimulation'
import type { CrmSimulationCatalog, CrmSimulationSettings, CrmSyncResult } from '../lib/crmSimulation'
import { CrmSyncLogModal } from './CrmSyncLogModal'
import type { CrmSyncRun } from './CrmSyncLogModal'
import { LinearProgress, Spinner } from './Progress'

interface CrmSimulationControlProps {
  /** Called after the demonstration inquiry is created so the queue reloads. */
  onInquiryCreated: () => void
  /** Routes the outcome to the shared triage snackbar. */
  onNotify: (message: string, variant: 'info' | 'error') => void
}

type PendingAction = 'apply' | 'run' | null

function resultMessage(result: CrmSyncResult): string {
  const attemptLabel = `${result.attempts} ${result.attempts === 1 ? 'attempt' : 'attempts'}`
  switch (result.outcome) {
    case 'Success':
      return `Inquiry #${result.inquiryId} created. CRM sync succeeded after ${attemptLabel}.`
    case 'Failed':
      return `Inquiry #${result.inquiryId} created and kept. CRM sync failed after ${attemptLabel}; the stored inquiry is unaffected.`
    case 'TimedOut':
      return `Inquiry #${result.inquiryId} created and kept. CRM sync timed out after ${attemptLabel}; the stored inquiry is unaffected.`
    case 'Cancelled':
      return `Inquiry #${result.inquiryId} created and kept. CRM sync was cancelled after ${attemptLabel}; the stored inquiry is unaffected.`
  }
}

/**
 * Dev-only CRM demonstrator: choose a runtime outcome, then run one real inquiry
 * through the production POST endpoint and read back only safe CRM metadata. The
 * demo inquiry appears in the normal queue; server logs carry the attempt trail.
 * Renders nothing unless the shell set window.__crmSimulationTools.
 */
export function CrmSimulationControl({ onInquiryCreated, onNotify }: CrmSimulationControlProps) {
  const enabled = crmSimulationToolsEnabled()
  const [catalog, setCatalog] = useState<CrmSimulationCatalog | null>(null)
  const [settings, setSettings] = useState<CrmSimulationSettings | null>(null)
  const [pending, setPending] = useState<PendingAction>(null)
  const [lastRun, setLastRun] = useState<CrmSyncRun | null>(null)
  const [logOpen, setLogOpen] = useState(false)
  const onInquiryCreatedRef = useRef(onInquiryCreated)
  onInquiryCreatedRef.current = onInquiryCreated
  const onNotifyRef = useRef(onNotify)
  onNotifyRef.current = onNotify

  useEffect(() => {
    if (!enabled) {
      return
    }
    const controller = new AbortController()
    void (async () => {
      const loaded = await fetchCrmSimulation(controller.signal)
      if (loaded) {
        setCatalog(loaded)
        setSettings(loaded.settings)
      }
    })()
    return () => controller.abort()
  }, [enabled])

  if (!enabled || !catalog || !settings) {
    return null
  }

  const busy = pending !== null
  const selectedMode = catalog.modes.find((mode) => mode.name === settings.mode)

  const apply = async (): Promise<boolean> => {
    setPending('apply')
    const updated = await updateCrmSimulation(settings)
    if (!updated) {
      onNotifyRef.current('CRM behavior could not be updated. Check the values and try again.', 'error')
      setPending(null)
      return false
    }
    setSettings(updated)
    const label = catalog.modes.find((mode) => mode.name === updated.mode)
    onNotifyRef.current(
      `${updated.mode} is active for new inquiries. ${label?.description ?? ''}`.trim(),
      'info',
    )
    setPending(null)
    return true
  }

  const runDemo = async () => {
    setPending('run')

    const updated = await updateCrmSimulation(settings)
    if (!updated) {
      onNotifyRef.current('CRM behavior could not be updated; no inquiry was created.', 'error')
      setPending(null)
      return
    }
    setSettings(updated)

    const inquiryId = await createCrmDemoInquiry()
    if (inquiryId === null) {
      onNotifyRef.current('The demonstration inquiry could not be created.', 'error')
      setPending(null)
      return
    }

    onInquiryCreatedRef.current()
    const result = await fetchCrmSyncResult(inquiryId)
    if (result) {
      setLastRun({ result, settings: updated })
      onNotifyRef.current(resultMessage(result), result.outcome === 'Success' ? 'info' : 'error')
    } else {
      onNotifyRef.current(
        `Inquiry #${inquiryId} created, but its CRM result was unavailable. Check the server logs.`,
        'error',
      )
    }
    setPending(null)
  }

  return (
    <aside className="dev-crm" aria-label="Developer CRM simulation tools" aria-busy={busy}>
      {busy && (
        <div className="dev-tool-progress">
          <LinearProgress
            label={pending === 'run' ? 'Running the CRM simulation' : 'Applying CRM behavior'}
          />
        </div>
      )}
      <div className="dev-crm-heading">
        <span className="dev-tools-tag">DEV</span>
        <div>
          <strong>CRM delivery</strong>
          <p>{selectedMode?.description}</p>
        </div>
      </div>

      <label className="dev-tools-field">
        <span>Behavior</span>
        <select
          value={settings.mode}
          disabled={busy}
          onChange={(event) => {
            const selected = catalog.modes.find((mode) => mode.name === event.target.value)
            if (selected) {
              setSettings((current) => (current ? { ...current, mode: selected.name } : current))
            }
          }}
        >
          {catalog.modes.map((mode) => (
            <option key={mode.name} value={mode.name}>
              {mode.name}
            </option>
          ))}
        </select>
      </label>

      {settings.mode === 'TransientThenSuccess' && (
        <label className="dev-tools-field dev-tools-field--number">
          <span>Failures first</span>
          <input
            type="number"
            min="0"
            max="3"
            value={settings.transientFailuresBeforeSuccess}
            disabled={busy}
            onChange={(event) => {
              const failures = event.target.valueAsNumber
              if (Number.isInteger(failures)) {
                setSettings((current) =>
                  current ? { ...current, transientFailuresBeforeSuccess: failures } : current,
                )
              }
            }}
          />
        </label>
      )}

      <label className="dev-tools-field dev-tools-field--number">
        <span>Latency (ms)</span>
        <input
          type="number"
          min="0"
          max="400"
          step="25"
          value={settings.latencyMilliseconds}
          disabled={busy}
          onChange={(event) => {
            const latency = event.target.valueAsNumber
            if (Number.isInteger(latency)) {
              setSettings((current) =>
                current ? { ...current, latencyMilliseconds: latency } : current,
              )
            }
          }}
        />
      </label>

      <div className="dev-crm-actions">
        <button type="button" onClick={() => void apply()} disabled={busy}>
          {pending === 'apply' && <Spinner size={16} />}
          Apply
        </button>
        <button type="button" className="dev-crm-run" onClick={() => void runDemo()} disabled={busy}>
          {pending === 'run' && <Spinner size={16} />}
          Run demo inquiry
        </button>
        {lastRun && (
          <button
            type="button"
            className="dev-crm-log"
            onClick={() => setLogOpen(true)}
            disabled={busy}
          >
            View sync log
          </button>
        )}
      </div>

      <CrmSyncLogModal run={logOpen ? lastRun : null} onClose={() => setLogOpen(false)} />
    </aside>
  )
}
