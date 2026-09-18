import { useEffect, useRef, useState } from 'react'
import {
  armIntakeFault,
  fetchIntakeFault,
  intakeFaultToolsEnabled,
  submitFaultDemoInquiry,
} from '../lib/intakeFault'
import type { IntakeFaultState } from '../lib/intakeFault'

interface IntakeFaultControlProps {
  /** Called after the demonstration submission so the queue reloads and visibly excludes the failed row. */
  onChanged: () => void
}

/**
 * Dev-only intake fault demonstrator: arm the next create to fail before any write,
 * then run one real inquiry through the production POST endpoint and observe a
 * genuine 500 with the sanitized traceId — and no stored row. This stages the
 * "backend/DB failure → never stored" branch of the troubleshooting answer. Renders
 * nothing unless the shell set window.__intakeFaultTools (so it is absent in
 * production and inert in the test harness, with no mount-time request when off).
 */
export function IntakeFaultControl({ onChanged }: IntakeFaultControlProps) {
  const enabled = intakeFaultToolsEnabled()
  const [state, setState] = useState<IntakeFaultState | null>(null)
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
      const loaded = await fetchIntakeFault(controller.signal)
      if (loaded) {
        setState(loaded)
      }
    })()
    return () => controller.abort()
  }, [enabled])

  if (!enabled || !state) {
    return null
  }

  const armAndSubmit = async () => {
    setBusy(true)
    setStatus('Arming one intake failure and submitting…')

    const armed = await armIntakeFault(1)
    if (!armed) {
      setStatus('The fault switch could not be armed; no submission was made.')
      setBusy(false)
      return
    }
    setState(armed)

    const result = await submitFaultDemoInquiry()
    onChangedRef.current()

    if (result === null) {
      setStatus('The demonstration submission could not be sent.')
    } else if (result.status >= 500) {
      const trace = result.traceId ? ` (traceId ${result.traceId})` : ''
      setStatus(
        `Submission failed with HTTP ${result.status}${trace}. No row was stored — the genuine backend-failure “never stored” case. The queue below does not contain it.`,
      )
    } else if (result.status === 201) {
      setStatus(
        'Submission unexpectedly succeeded (HTTP 201) and a row was stored — the armed fault was already consumed. Arm again to retry.',
      )
    } else {
      setStatus(
        `Submission returned HTTP ${result.status}; expected a 500 with no row stored — check the server logs.`,
      )
    }

    const refreshed = await fetchIntakeFault()
    if (refreshed) {
      setState(refreshed)
    }
    setBusy(false)
  }

  const disarm = async () => {
    setBusy(true)
    const cleared = await armIntakeFault(0)
    if (cleared) {
      setState(cleared)
      setStatus('Intake fault disarmed. New submissions are stored normally.')
    } else {
      setStatus('The fault switch could not be disarmed.')
    }
    setBusy(false)
  }

  return (
    <aside className="dev-crm dev-fault" aria-label="Developer intake fault tools">
      <div className="dev-crm-heading">
        <span className="dev-tools-tag">DEV</span>
        <div>
          <strong>Intake fault</strong>
          <p>
            Arm the next submission to fail before any write, staging a genuine 500 that stores no
            row.
          </p>
        </div>
      </div>

      <div className="dev-tools-field">
        <span>Switch</span>
        <span className="dev-fault-state">
          {state.armed > 0 ? `Armed (next ${state.armed})` : 'Inert'} · {state.totalInjected} injected
        </span>
      </div>

      <div className="dev-crm-actions">
        <button
          type="button"
          className="dev-crm-run"
          onClick={() => void armAndSubmit()}
          disabled={busy}
        >
          Arm one failure and submit
        </button>
        <button type="button" onClick={() => void disarm()} disabled={busy || state.armed === 0}>
          Disarm
        </button>
      </div>

      <span className="dev-tools-status" role="status" aria-live="polite">
        {status}
      </span>
    </aside>
  )
}
