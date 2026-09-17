import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { jsonResponse } from '../test/fetchDouble'
import type { RecordedRequest } from '../test/fetchDouble'
import {
  envelope,
  fixInq1,
  fixInq1Contacted,
  fixInq2,
  makeRows,
  problem400Validation,
  problem404,
} from '../test/fixtures'
import { renderIsland, uninstallActiveIsland } from '../test/islandTestKit'

afterEach(uninstallActiveIsland)

const user = userEvent.setup()

function putCall(calls: readonly RecordedRequest[]): RecordedRequest {
  return calls.find((call) => call.method === 'PUT')!
}

describe('IT-UI-015 apply issues one PUT with the canonical body and refreshes', () => {
  it('sends exactly {"status":"Contacted"}, announces saved, and refreshes the list', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, fixInq1Contacted()))
    double.queueResponse(jsonResponse(200, envelope([fixInq1Contacted()])))

    const callsBeforeSelect = double.calls.length
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    expect(double.calls.length).toBe(callsBeforeSelect)

    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    await screen.findByText(/status saved/i)
    const put = putCall(double.calls)
    expect(put.url).toBe('/api/inquiries/1/status')
    expect(put.headers['content-type']).toBe('application/json')
    expect(put.body).toBe('{"status":"Contacted"}')
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries')
    expect(double.calls.at(-1)?.method).toBe('GET')

    await waitFor(() =>
      expect(
        within(screen.getByRole('row', { name: /O'Neill/ })).getByText('Contacted', {
          selector: '.status-badge',
        }),
      ).toBeInTheDocument(),
    )
  })
})

describe('IT-UI-016 a row leaving the active filter disappears after refresh', () => {
  it('refreshes with the current filter and reconciles totals without reporting failure', async () => {
    const secondNewRow = { ...fixInq2(), status: 'New' as const }
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), secondNewRow]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, envelope([fixInq1(), secondNewRow], { totalCount: 2 })))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'New')
    await waitFor(() =>
      expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=New&page=1'),
    )

    double.queueResponse(jsonResponse(200, fixInq1Contacted()))
    double.queueResponse(jsonResponse(200, envelope([secondNewRow], { totalCount: 1 })))
    const oneill = screen.getByRole('row', { name: /O'Neill/ })
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    await waitFor(() =>
      expect(screen.queryByRole('row', { name: /O'Neill/ })).not.toBeInTheDocument(),
    )
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=New&page=1')
    expect(screen.getByText(/1 inquiry/, { selector: '.total-count' })).toBeInTheDocument()
    expect(await screen.findByText(/status saved/i)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
  })
})

describe('IT-UI-017 update emptying the current page navigates to the last page', () => {
  it('lands on page 1 with the reduced total instead of an empty dead end', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(
        jsonResponse(200, envelope(makeRows(20, 'Contacted'), { totalCount: 21 })),
      ),
    )
    await screen.findByRole('row', { name: /First1 / })

    double.queueResponse(
      jsonResponse(200, envelope(makeRows(20, 'Contacted'), { totalCount: 21 })),
    )
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Contacted')
    await waitFor(() =>
      expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=Contacted&page=1'),
    )

    const lastRow = { ...fixInq1(), id: 21, status: 'Contacted' as const }
    double.queueResponse(
      jsonResponse(200, envelope([lastRow], { page: 2, totalCount: 21 })),
    )
    await user.click(screen.getByRole('button', { name: /next/i }))
    await screen.findByRole('row', { name: /Avery/ })

    double.queueResponse(jsonResponse(200, { ...lastRow, status: 'Closed' }))
    double.queueResponse(jsonResponse(200, envelope([], { page: 2, totalCount: 20 })))
    double.queueResponse(
      jsonResponse(200, envelope(makeRows(20, 'Contacted'), { totalCount: 20 })),
    )
    const row = screen.getByRole('row', { name: /Avery/ })
    await user.selectOptions(
      within(row).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Closed',
    )
    await user.click(within(row).getByRole('button', { name: "Apply for Avery O'Neill" }))

    await screen.findByRole('row', { name: /First1 / })
    expect(screen.getByText(/page 1 of 1/i)).toBeInTheDocument()
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=Contacted&page=1')
  })
})

describe('IT-UI-018 same-status apply is a successful no-op', () => {
  it('announces saved and keeps the unchanged updatedDate', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, fixInq1()))
    await user.click(screen.getByRole('button', { name: /details for Avery O'Neill/i }))
    await screen.findByText('Email')

    double.queueResponse(jsonResponse(200, fixInq1()))
    double.queueResponse(jsonResponse(200, envelope([fixInq1()])))
    const callsBefore = double.calls.length
    const oneill = screen.getByRole('row', { name: /O'Neill/ })
    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    expect(await screen.findByText(/status saved/i)).toBeInTheDocument()
    expect(double.calls.length).toBe(callsBefore + 2) // PUT + refresh
    const panelUpdated = screen.getAllByText('2026-09-10')
    expect(panelUpdated.length).toBeGreaterThanOrEqual(3) // row created + detail created/updated
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
  })
})

describe('IT-UI-019 a pending apply blocks duplicate submissions', () => {
  it('issues exactly one PUT and re-enables after resolution', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })
    const apply = within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" })

    const pending = double.queueDeferred()
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    await user.click(apply)

    expect(apply).toBeDisabled()
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Pending',
    )
    await user.click(apply).catch(() => undefined)

    double.queueResponse(jsonResponse(200, envelope([fixInq1Contacted()])))
    await act(async () => {
      pending.resolve(jsonResponse(200, fixInq1Contacted()))
    })

    expect(double.calls.filter((call) => call.method === 'PUT')).toHaveLength(1)
    await waitFor(() => expect(apply).toBeEnabled())
  })
})

describe('IT-UI-020 a validation failure keeps the persisted status', () => {
  it('surfaces the failing field without echoing the attempted value and skips refresh', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(400, problem400Validation()))
    const callsBefore = double.calls.length
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toMatch(/status/i)
    expect(alert.textContent).not.toMatch(/saved/i)
    expect(
      within(screen.getByRole('row', { name: /O'Neill/ })).getByText('New', {
        selector: '.status-badge',
      }),
    ).toBeInTheDocument()
    expect(double.calls.length).toBe(callsBefore + 1) // only the PUT, no refresh
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })
})

describe('IT-UI-021 a 404 update announces gone and refreshes the row away', () => {
  it('reports the missing record and reconciles the list', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(404, problem404()))
    double.queueResponse(jsonResponse(200, envelope([fixInq2()])))
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    expect(await screen.findByText(/no longer exists|not found|gone/i)).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('row', { name: /O'Neill/ })).not.toBeInTheDocument(),
    )
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })
})

describe('IT-UI-022 a network failure keeps the persisted status and allows retry', () => {
  it('announces a recoverable failure, then a second apply succeeds', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })
    const apply = within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" })
    const rowSelect = within(oneill).getByRole('combobox', {
      name: "Status for Avery O'Neill",
    })

    double.queueRejection(new TypeError('Failed to fetch'))
    await user.selectOptions(rowSelect, 'Contacted')
    await user.click(apply)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/could not be saved|try again/i)
    expect(alert.textContent).not.toContain('Failed to fetch')
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
    expect(
      within(screen.getByRole('row', { name: /O'Neill/ })).getByText('New', {
        selector: '.status-badge',
      }),
    ).toBeInTheDocument()
    await waitFor(() => expect(apply).toBeEnabled())

    double.queueResponse(jsonResponse(200, fixInq1Contacted()))
    double.queueResponse(jsonResponse(200, envelope([fixInq1Contacted()])))
    await user.click(apply)

    expect(await screen.findByText(/status saved/i)).toBeInTheDocument()
    await waitFor(() =>
      expect(
        within(screen.getByRole('row', { name: /O'Neill/ })).getByText('Contacted', {
          selector: '.status-badge',
        }),
      ).toBeInTheDocument(),
    )
  })
})

describe('IT-UI-023 a successful save with a failed refresh reports both parts', () => {
  it('says saved and out of date, keeping the PUT-applied row and prior totals', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()], { totalCount: 3 }))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, fixInq1Contacted()))
    double.queueRejection(new TypeError('Failed to fetch'))
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    const status = await screen.findByRole('status')
    await waitFor(() => expect(status.textContent).toMatch(/saved/i))
    expect(status.textContent).toMatch(/refresh failed|out of date/i)
    expect(status.textContent).not.toMatch(/^The status could not be saved/)
    expect(
      within(screen.getByRole('row', { name: /O'Neill/ })).getByText('Contacted', {
        selector: '.status-badge',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText(/3 inquiries/)).toBeInTheDocument()
  })
})
