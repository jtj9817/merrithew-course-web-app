import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { IntakeFaultControl } from './IntakeFaultControl'
import { jsonResponse, installFetchDouble } from '../test/fetchDouble'
import type { FetchDouble, RecordedRequest } from '../test/fetchDouble'

// The intake fault control renders only when the shell set its flag; tests install
// it directly so the flag state is explicit per case.
declare global {
  interface Window {
    __intakeFaultTools?: boolean
  }
}

function stateResponse(armed: number, totalInjected: number) {
  return jsonResponse(200, { armed, totalInjected })
}

function putCall(calls: readonly RecordedRequest[]) {
  const put = calls.find((call) => call.url === '/api/dev/intake-fault' && call.method === 'PUT')
  expect(put).toBeDefined()
  return put!
}

describe('IntakeFaultControl', () => {
  let double: FetchDouble

  beforeEach(() => {
    window.__intakeFaultTools = true
    double = installFetchDouble()
  })

  afterEach(() => {
    double.uninstall()
    delete window.__intakeFaultTools
    vi.restoreAllMocks()
    cleanup()
  })

  it('renders nothing until the shell enables the tools', () => {
    window.__intakeFaultTools = false
    const { container } = render(<IntakeFaultControl onChanged={() => {}} />)
    expect(container).toBeEmptyDOMElement()
    expect(double.calls).toHaveLength(0) // no mount-time request when the flag is off
  })

  it('loads the switch state and offers the arm-and-submit action', async () => {
    double.queueResponse(stateResponse(0, 0))
    render(<IntakeFaultControl onChanged={() => {}} />)

    expect(
      await screen.findByRole('button', { name: 'Arm one failure and submit' }),
    ).toBeInTheDocument()
    expect(double.calls[0]?.url).toBe('/api/dev/intake-fault')
    expect(screen.getByText(/Inert · 0 injected/)).toBeInTheDocument()
    // Disarm is disabled while the switch is inert.
    expect(screen.getByRole('button', { name: 'Disarm' })).toBeDisabled()
  })

  it('arms one failure, submits, and reports a genuine 500 with no row stored', async () => {
    double.queueResponse(stateResponse(0, 0))
    const onChanged = vi.fn()
    render(<IntakeFaultControl onChanged={onChanged} />)

    await screen.findByRole('button', { name: 'Arm one failure and submit' })
    double.queueResponse(stateResponse(1, 0)) // PUT armCount=1
    double.queueResponse(jsonResponse(500, { traceId: '0HABC123', status: 500 })) // POST /api/inquiries
    double.queueResponse(stateResponse(0, 1)) // GET refresh

    await userEvent.click(screen.getByRole('button', { name: 'Arm one failure and submit' }))

    const status = await screen.findByText(/Submission failed with HTTP 500 \(traceId 0HABC123\)/)
    expect(status).toBeInTheDocument()
    expect(status).toHaveTextContent(/No row was stored/)
    expect(onChanged).toHaveBeenCalled()

    const post = double.calls.find((call) => call.url === '/api/inquiries' && call.method === 'POST')
    expect(post).toBeDefined()
    expect(putCall(double.calls).body).toContain('"armCount":1')
    // The readout reflects the consumed fault.
    expect(await screen.findByText(/Inert · 1 injected/)).toBeInTheDocument()
  })

  it('reports a failed arm without submitting anything', async () => {
    double.queueResponse(stateResponse(0, 0))
    render(<IntakeFaultControl onChanged={() => {}} />)

    await screen.findByRole('button', { name: 'Arm one failure and submit' })
    double.queueResponse(jsonResponse(400, {})) // PUT rejected

    await userEvent.click(screen.getByRole('button', { name: 'Arm one failure and submit' }))

    expect(await screen.findByText(/could not be armed; no submission was made/)).toBeInTheDocument()
    expect(double.calls.find((call) => call.url === '/api/inquiries')).toBeUndefined()
  })
})
