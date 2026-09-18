import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ReconciliationPanel } from './ReconciliationPanel'
import { jsonResponse, installFetchDouble } from '../test/fetchDouble'
import type { FetchDouble } from '../test/fetchDouble'

// The reconciliation panel renders only when the shell set its flag; tests install
// it directly so the flag state is explicit per case.
declare global {
  interface Window {
    __reconciliationTools?: boolean
  }
}

function report(overrides: Partial<Record<string, unknown>> = {}) {
  return jsonResponse(200, {
    countByStatus: [
      { status: 'New', count: 5 },
      { status: 'Contacted', count: 5 },
      { status: 'Pending', count: 5 },
      { status: 'Registered', count: 5 },
      { status: 'Closed', count: 6 },
    ],
    totalCount: 26,
    last7DaysCount: 7,
    duplicateEmailGroups: [{ normalizedEmail: 'hannah.becker@example.com', occurrenceCount: 2 }],
    ...overrides,
  })
}

describe('ReconciliationPanel', () => {
  let double: FetchDouble

  beforeEach(() => {
    window.__reconciliationTools = true
    double = installFetchDouble()
  })

  afterEach(() => {
    double.uninstall()
    delete window.__reconciliationTools
    vi.restoreAllMocks()
    cleanup()
  })

  it('renders nothing until the shell enables the tools', () => {
    window.__reconciliationTools = false
    const { container } = render(<ReconciliationPanel reloadToken={0} />)
    expect(container).toBeEmptyDOMElement()
    expect(double.calls).toHaveLength(0) // no mount-time request when the flag is off
  })

  it('loads and renders the counts, window, and duplicate group', async () => {
    double.queueResponse(report())
    render(<ReconciliationPanel reloadToken={0} />)

    expect(await screen.findByText(/26 stored · 7 in the last 7 days/)).toBeInTheDocument()
    expect(double.calls[0]?.url).toBe('/api/dev/reconciliation')
    expect(screen.getByText('New')).toBeInTheDocument()
    expect(screen.getByText('Closed')).toBeInTheDocument()
    expect(screen.getByText(/hannah\.becker@example\.com ×2/)).toBeInTheDocument()
  })

  it('refetches when the reload token changes', async () => {
    double.queueResponse(report())
    const { rerender } = render(<ReconciliationPanel reloadToken={0} />)
    await screen.findByText(/26 stored/)

    double.queueResponse(report({ totalCount: 27, last7DaysCount: 8 }))
    rerender(<ReconciliationPanel reloadToken={1} />)

    expect(await screen.findByText(/27 stored · 8 in the last 7 days/)).toBeInTheDocument()
    expect(
      double.calls.filter((call) => call.url === '/api/dev/reconciliation'),
    ).toHaveLength(2)
  })

  it('shows an unavailable message when the report cannot be fetched', async () => {
    double.queueResponse(jsonResponse(500, {}))
    render(<ReconciliationPanel reloadToken={0} />)

    expect(await screen.findByText('Reconciliation report unavailable.')).toBeInTheDocument()
  })
})
