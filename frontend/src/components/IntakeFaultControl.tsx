import { useEffect, useRef, useState } from 'react'
import {
  armIntakeFault,
  fetchIntakeFault,
  intakeFaultToolsEnabled,
  submitFaultDemoInquiry,
} from '../lib/intakeFault'
import type { IntakeFaultState } from '../lib/intakeFault'
import { LinearProgress, Spinner } from './Progress'

interface IntakeFaultControlProps {
  /** Called after the demonstration submission so the queue reloads and visibly excludes the failed row. */
  onChanged: () => void
  /** Routes the outcome to the shared triage snackbar. */
  onNotify: (message: string, variant: 'info' | 'error') => void
}

type PendingAction = 'submit' | 'disarm' | null

/**
 * Dev-only intake fault demonstrator: arm the next create to fail before any write,
 * then run one real inquiry through the production POST endpoint and observe a
 * genuine 500 with the sanitized traceId, and no stored row. This stages the
 * "backend/DB failure → never stored" branch of the troubleshooting answer. Renders
 * nothing unless the shell set window.__intakeFaultTools (so it is absent in
 * production and inert in the test harness, with no mount-time request when off).
 */
export function IntakeFaultControl({ onChanged, onNotify }: IntakeFaultControlProps) {
  const enabled = intakeFaultToolsEnabled()
  const [state, setState] = useState<IntakeFaultState | null>(null)
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

  const busy = pending !== null

  const armAndSubmit = async () => {
    setPending('submit')

    const armed = await armIntakeFault(1)
    if (!armed) {
      onNotifyRef.current('The fault switch could not be armed; no submission was made.', 'error')
      setPending(null)
      return
    }
    setState(armed)

    const result = await submitFaultDemoInquiry()
    onChangedRef.current()

    if (result === null) {
      onNotifyRef.current('The demonstration submission could not be sent.', 'error')
    } else if (result.status >= 500) {
      const trace = result.traceId ? ` (traceId ${result.traceId})` : ''
      onNotifyRef.current(
        `Submission failed with HTTP ${result.status}${trace}. No row was stored; the genuine backend-failure “never stored” case. The queue below does not contain it.`,
        'error',
      )
    } else if (result.status === 201) {
      onNotifyRef.current(
        'Submission unexpectedly succeeded (HTTP 201) and a row was stored; the armed fault was already consumed. Arm again to retry.',
        'error',
      )
    } else {
      onNotifyRef.current(
        `Submission returned HTTP ${result.status}; expected a 500 with no row stored. Check the server logs.`,
        'error',
      )
    }

    const refreshed = await fetchIntakeFault()
    if (refreshed) {
      setState(refreshed)
    }
    setPending(null)
  }

  const disarm = async () => {
    setPending('disarm')
    const cleared = await armIntakeFault(0)
    if (cleared) {
      setState(cleared)
      onNotifyRef.current('Intake fault disarmed. New submissions are stored normally.', 'info')
    } else {
      onNotifyRef.current('The fault switch could not be disarmed.', 'error')
    }
    setPending(null)
  }

  return (
    <aside className="dev-crm dev-fault" aria-label="Developer intake fault tools" aria-busy={busy}>
      {busy && (
        <div className="dev-tool-progress">
          <LinearProgress
            label={pending === 'submit' ? 'Submitting the intake fault demo' : 'Disarming the intake fault'}
          />
        </div>
      )}
      <div className="dev-crm-heading">
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
          {pending === 'submit' && <Spinner size={16} />}
          Arm one failure and submit
        </button>
        <button type="button" onClick={() => void disarm()} disabled={busy || state.armed === 0}>
          {pending === 'disarm' && <Spinner size={16} />}
          Disarm
        </button>
      </div>
    </aside>
  )
}
