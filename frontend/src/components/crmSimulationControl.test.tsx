import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Mock } from 'vitest'
import { CrmSimulationControl } from './CrmSimulationControl'
import { jsonResponse, installFetchDouble } from '../test/fetchDouble'
import type { FetchDouble, RecordedRequest } from '../test/fetchDouble'

type NotifyFn = (message: string, variant: 'info' | 'error') => void

// The CRM control renders only when the shell set its flag; tests install it
// directly so the flag state is explicit per case.
declare global {
  interface Window {
    __crmSimulationTools?: boolean
  }
}

function catalogResponse() {
  return jsonResponse(200, {
    settings: { mode: 'Success', transientFailuresBeforeSuccess: 2, latencyMilliseconds: 100 },
    modes: [
      { name: 'Success', description: 'One successful CRM attempt.' },
      { name: 'PermanentFailure', description: 'One permanent rejection is not retried.' },
    ],
  })
}

function createdResponse(id: number) {
  return jsonResponse(201, {
    id,
    firstName: 'CRM',
    lastName: 'Demo',
    email: 'crm.demo@example.test',
    phone: null,
    courseName: 'CRM integration demonstration',
    preferredLocation: 'Toronto',
    message: 'Development-only CRM delivery demonstration.',
    status: 'New',
    createdDate: '2026-09-17T10:00:00Z',
    updatedDate: '2026-09-17T10:00:00Z',
  })
}

function putCall(calls: readonly RecordedRequest[]) {
  const put = calls.find(call => call.url === '/api/dev/crm-simulation' && call.method === 'PUT')
  expect(put).toBeDefined()
  return put!
}

describe('CrmSimulationControl', () => {
  let double: FetchDouble
  let notify: Mock<NotifyFn>

  beforeEach(() => {
    window.__crmSimulationTools = true
    double = installFetchDouble()
    notify = vi.fn<NotifyFn>()
  })

  afterEach(() => {
    double.uninstall()
    delete window.__crmSimulationTools
    vi.restoreAllMocks()
    cleanup()
  })

  it('renders nothing until the shell enables the tools', () => {
    window.__crmSimulationTools = false
    const { container } = render(
      <CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />,
    )
    expect(container).toBeEmptyDOMElement()
    expect(double.calls).toHaveLength(0) // no mount-time request when the flag is off
  })

  it('loads the catalog and shows the selected mode description', async () => {
    double.queueResponse(catalogResponse())
    render(<CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />)

    expect(await screen.findByText('One successful CRM attempt.')).toBeInTheDocument()
    expect(double.calls[0]?.url).toBe('/api/dev/crm-simulation')
    expect(screen.getByRole('button', { name: 'Run demo inquiry' })).toBeInTheDocument()
  })

  it('shows an in-flight progress indicator while a run is pending', async () => {
    double.queueResponse(catalogResponse())
    render(<CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />)

    await screen.findByText('One successful CRM attempt.')
    const pending = double.queueDeferred() // hold the PUT so the control stays busy

    await userEvent.click(screen.getByRole('button', { name: 'Run demo inquiry' }))

    expect(
      await screen.findByRole('progressbar', { name: 'Running the CRM simulation' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Run demo inquiry/ })).toBeDisabled()

    // Let the request fail cleanly so the pending state resolves.
    pending.resolve(jsonResponse(400, {}))
    await waitFor(() => expect(screen.queryByRole('progressbar')).toBeNull())
  })

  it('runs one inquiry through the real endpoint and reports the sync outcome', async () => {
    double.queueResponse(catalogResponse())
    const onChanged = vi.fn()
    render(<CrmSimulationControl onInquiryCreated={onChanged} onNotify={notify} />)

    await screen.findByText('One successful CRM attempt.')
    double.queueResponse(jsonResponse(200, { mode: 'Success', transientFailuresBeforeSuccess: 2, latencyMilliseconds: 100 })) // PUT apply
    double.queueResponse(createdResponse(41)) // POST /api/inquiries
    double.queueResponse(jsonResponse(200, { inquiryId: 41, mode: 'Success', outcome: 'Failed', attempts: 4 })) // GET result

    await userEvent.click(screen.getByRole('button', { name: 'Run demo inquiry' }))

    await waitFor(() =>
      expect(notify).toHaveBeenCalledWith(
        expect.stringMatching(/#41 created and kept\. CRM sync failed after 4 attempts/),
        'error',
      ),
    )
    expect(onChanged).toHaveBeenCalled()
    const post = double.calls.find(call => call.url === '/api/inquiries' && call.method === 'POST')
    expect(post?.body).toContain('CRM integration demonstration')
    expect(putCall(double.calls).body).toContain('"mode":"Success"')
  })

  it('announces a successful sync as an info notification', async () => {
    double.queueResponse(catalogResponse())
    render(<CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />)

    await screen.findByText('One successful CRM attempt.')
    double.queueResponse(jsonResponse(200, { mode: 'Success', transientFailuresBeforeSuccess: 2, latencyMilliseconds: 100 }))
    double.queueResponse(createdResponse(42))
    double.queueResponse(jsonResponse(200, { inquiryId: 42, mode: 'Success', outcome: 'Success', attempts: 1 }))

    await userEvent.click(screen.getByRole('button', { name: 'Run demo inquiry' }))

    await waitFor(() =>
      expect(notify).toHaveBeenCalledWith(
        expect.stringMatching(/#42 created\. CRM sync succeeded after 1 attempt\./),
        'info',
      ),
    )
  })

  it('explains when the sync result is unavailable but the inquiry exists', async () => {
    double.queueResponse(catalogResponse())
    render(<CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />)

    await screen.findByText('One successful CRM attempt.')
    double.queueResponse(jsonResponse(200, { mode: 'Success', transientFailuresBeforeSuccess: 2, latencyMilliseconds: 100 }))
    double.queueResponse(createdResponse(7))
    double.queueResponse(jsonResponse(404, { }))

    await userEvent.click(screen.getByRole('button', { name: 'Run demo inquiry' }))

    await waitFor(() =>
      expect(notify).toHaveBeenCalledWith(
        expect.stringMatching(/#7 created, but its CRM result was unavailable/),
        'error',
      ),
    )
  })

  it('reports a failed behavior update without creating an inquiry', async () => {
    double.queueResponse(catalogResponse())
    render(<CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />)

    await screen.findByText('One successful CRM attempt.')
    double.queueResponse(jsonResponse(400, { }))

    await userEvent.click(screen.getByRole('button', { name: 'Run demo inquiry' }))

    await waitFor(() =>
      expect(notify).toHaveBeenCalledWith(
        expect.stringMatching(/could not be updated; no inquiry was created/),
        'error',
      ),
    )
    expect(double.calls.find(call => call.url === '/api/inquiries')).toBeUndefined()
  })

  it('applies a selected behavior without creating an inquiry', async () => {
    double.queueResponse(catalogResponse())
    render(<CrmSimulationControl onInquiryCreated={() => {}} onNotify={notify} />)

    await screen.findByText('One successful CRM attempt.')
    double.queueResponse(jsonResponse(200, { mode: 'PermanentFailure', transientFailuresBeforeSuccess: 2, latencyMilliseconds: 100 }))

    await userEvent.selectOptions(screen.getByRole('combobox'), 'PermanentFailure')
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }))

    await waitFor(() =>
      expect(notify).toHaveBeenCalledWith(
        expect.stringMatching(/PermanentFailure is active for new inquiries/),
        'info',
      ),
    )
    expect(double.calls.find(call => call.url === '/api/inquiries')).toBeUndefined()
    await waitFor(() => expect(putCall(double.calls).body).toContain('"mode":"PermanentFailure"'))
  })
})
