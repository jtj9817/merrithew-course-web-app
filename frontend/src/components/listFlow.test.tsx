import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { jsonResponse } from '../test/fetchDouble'
import type { DeferredHandle } from '../test/fetchDouble'
import { envelope, fixInq1, fixInq2, makeRows } from '../test/fixtures'
import { renderIsland, uninstallActiveIsland } from '../test/islandTestKit'

afterEach(uninstallActiveIsland)

const user = userEvent.setup()

describe('IT-UI-001 initial fetch renders loading then rows in envelope order', () => {
  it('shows a loading indicator while pending and renders envelope rows after resolve', async () => {
    let pending!: DeferredHandle
    const { double } = renderIsland((d) => {
      pending = d.queueDeferred()
    })

    expect(screen.getByText(/loading/i)).toBeInTheDocument()
    expect(screen.queryByRole('cell')).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()

    await act(async () => {
      pending.resolve(jsonResponse(200, envelope([fixInq1(), fixInq2()])))
    })

    const oneill = screen.getByRole('row', { name: /O'Neill/ })
    const fernandez = screen.getByRole('row', { name: /Fernández/ })
    expect(
      oneill.compareDocumentPosition(fernandez) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()

    expect(within(oneill).getByText('Avery')).toBeInTheDocument()
    expect(within(oneill).getByText('New', { selector: '.status-badge' })).toBeInTheDocument()
    expect(within(oneill).getByText('2026-09-10')).toBeInTheDocument()
    expect(
      within(fernandez).getByText('Contacted', { selector: '.status-badge' }),
    ).toBeInTheDocument()
    expect(screen.getByText(/2 inquiries/)).toBeInTheDocument()

    expect(double.calls[0]?.url).toBe('/api/inquiries')
    expect(double.calls[0]?.method).toBe('GET')

    for (const header of ['First name', 'Last name', 'Course', 'Status', 'Created']) {
      screen.getByRole('columnheader', { name: header })
    }
  })
})

describe('IT-UI-002 empty store renders a distinct empty message', () => {
  it('distinguishes empty store from loading and error', async () => {
    renderIsland((d) => d.queueResponse(jsonResponse(200, envelope([]))))

    await screen.findByText(/no inquiries yet/i)

    expect(screen.queryByText(/loading/i)).not.toBeInTheDocument()
    expect(screen.getByText(/0 inquiries/)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
    expect(screen.queryByRole('cell')).not.toBeInTheDocument()
  })
})

describe('IT-UI-003 filtered empty result renders a filter-specific message', () => {
  it('distinguishes no-match from the empty-store message', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, envelope([])))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Contacted')

    await screen.findByText(/no inquiries match this filter/i)
    expect(screen.queryByText(/no inquiries yet/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('row', { name: /O'Neill/ })).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=Contacted&page=1')
  })
})

describe('IT-UI-004 network failure shows a recoverable error and Retry works', () => {
  it('announces a generic error, then Retry reloads and clears it', async () => {
    const { double } = renderIsland((d) =>
      d.queueRejection(new TypeError('Failed to fetch')),
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/could not be loaded|went wrong/i)
    expect(alert.textContent).not.toContain('Failed to fetch')
    expect(screen.queryByRole('cell')).not.toBeInTheDocument()

    double.queueResponse(jsonResponse(200, envelope([fixInq1()])))
    await user.click(screen.getByRole('button', { name: /retry/i }))

    await screen.findByRole('row', { name: /O'Neill/ })
    expect(screen.queryByText(/could not be loaded/i)).not.toBeInTheDocument()
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries')
  })
})

describe('IT-UI-005 filter change resets page and requests once', () => {
  it('issues exactly one request with the new filter and page 1', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope(makeRows(20), { totalCount: 45 }))),
    )
    await screen.findByRole('row', { name: /First1 / })

    double.queueResponse(
      jsonResponse(200, envelope(makeRows(2, 'New', 21), { page: 2, totalCount: 45 })),
    )
    await user.click(screen.getByRole('button', { name: /next/i }))
    await screen.findByRole('row', { name: /First21/ })

    double.queueResponse(jsonResponse(200, envelope([fixInq1()])))
    const callsBefore = double.calls.length
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Closed')

    await screen.findByRole('row', { name: /O'Neill/ })
    expect(double.calls.length).toBe(callsBefore + 1)
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=Closed&page=1')

    const filter = screen.getByLabelText('Filter by status') as HTMLSelectElement
    expect(Array.from(filter.options).map((option) => option.text)).toEqual([
      'All statuses',
      'New',
      'Contacted',
      'Pending',
      'Registered',
      'Closed',
    ])
  })
})

describe('IT-UI-006 returning to All statuses omits the status parameter', () => {
  it('requests the unfiltered list and renders its rows', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq2()]))),
    )
    await screen.findByRole('row', { name: /Fernández/ })

    double.queueResponse(jsonResponse(200, envelope([fixInq2()], { totalCount: 1 })))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Contacted')
    await waitFor(() =>
      expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=Contacted&page=1'),
    )

    double.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()])))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'All')

    await screen.findByRole('row', { name: /O'Neill/ })
    expect(double.calls.at(-1)?.url).not.toContain('status')
  })
})

describe('IT-UI-007 paging follows the response envelope', () => {
  it('requests adjacent pages and reflects the envelope page indicator', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope(makeRows(20, 'New'), { totalCount: 45 }))),
    )
    await screen.findByRole('row', { name: /First1 / })

    double.queueResponse(jsonResponse(200, envelope(makeRows(20, 'New'), { totalCount: 45 })))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'New')
    await waitFor(() =>
      expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=New&page=1'),
    )
    await screen.findByRole('row', { name: /First20 / })

    double.queueResponse(
      jsonResponse(200, envelope(makeRows(20, 'New', 21), { page: 2, totalCount: 45 })),
    )
    await user.click(screen.getByRole('button', { name: /next/i }))
    await screen.findByRole('row', { name: /First21/ })
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=New&page=2')
    expect(screen.getByText(/page 2 of 3/i)).toBeInTheDocument()
    expect(screen.getByText(/45 inquiries/)).toBeInTheDocument()

    double.queueResponse(jsonResponse(200, envelope(makeRows(20, 'New'), { totalCount: 45 })))
    await user.click(screen.getByRole('button', { name: /previous/i }))
    await screen.findByRole('row', { name: /First1 / })
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?status=New&page=1')

    double.queueResponse(
      jsonResponse(200, envelope(makeRows(5, 'New', 41), { page: 3, totalCount: 45 })),
    )
    await user.click(screen.getByRole('button', { name: /next/i }))
    await screen.findByRole('row', { name: /First41/ })
    expect(screen.getByText(/page 3 of 3/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled()
  })
})

describe('IT-UI-008 sort control emits exact spellings and follows response order', () => {
  it('requests createdDateAsc then createdDateDesc and renders each envelope order', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, envelope([fixInq2(), fixInq1()])))
    await user.selectOptions(screen.getByLabelText('Sort order'), 'createdDateAsc')
    await screen.findByText(/page 1 of 1/i)
    expect(double.calls.at(-1)?.url).toContain('sort=createdDateAsc')
    const fernandez = screen.getByRole('row', { name: /Fernández/ })
    const oneill = screen.getByRole('row', { name: /O'Neill/ })
    expect(
      fernandez.compareDocumentPosition(oneill) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()

    double.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()])))
    await user.selectOptions(screen.getByLabelText('Sort order'), 'createdDateDesc')
    await waitFor(() => expect(double.calls.at(-1)?.url).toContain('sort=createdDateDesc'))
    const oneillDesc = await screen.findByRole('row', { name: /O'Neill/ })
    const fernandezDesc = screen.getByRole('row', { name: /Fernández/ })
    expect(
      oneillDesc.compareDocumentPosition(fernandezDesc) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
  })
})

describe('IT-UI-009 empty page beyond the end reconciles to the last page', () => {
  it('navigates to page 1 and refetches when the page emptied under pagination', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope(makeRows(20), { totalCount: 45 }))),
    )
    await screen.findByRole('row', { name: /First1 / })

    double.queueResponse(jsonResponse(200, envelope([], { page: 2, totalCount: 20 })))
    double.queueResponse(jsonResponse(200, envelope(makeRows(20), { totalCount: 20 })))
    await user.click(screen.getByRole('button', { name: /next/i }))

    await screen.findByRole('row', { name: /First1 / })
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?page=1')
    expect(screen.getByText(/page 1 of 1/i)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
    expect(screen.queryByText(/no inquiries/i)).not.toBeInTheDocument()
  })
})

describe('IT-UI-010 stale list responses never overwrite newer state', () => {
  it('drops an older resolution that arrives after a newer page', async () => {
    let older!: DeferredHandle
    const { double } = renderIsland((d) => {
      older = d.queueDeferred()
    })

    const newer = double.queueDeferred()
    await user.click(screen.getByRole('button', { name: /next/i }))
    await act(async () => {
      newer.resolve(
        jsonResponse(200, envelope(makeRows(2, 'New', 21), { page: 2, totalCount: 45 })),
      )
    })
    await screen.findByRole('row', { name: /First21/ })

    await act(async () => {
      older.resolve(jsonResponse(200, envelope([fixInq1(), fixInq2()], { totalCount: 45 })))
    })

    expect(screen.queryByRole('row', { name: /O'Neill/ })).not.toBeInTheDocument()
    expect(screen.getByRole('row', { name: /First21/ })).toBeInTheDocument()
    expect(screen.getByRole('row', { name: /First22/ })).toBeInTheDocument()
    expect(screen.getByText(/45 inquiries/)).toBeInTheDocument()
  })
})

describe('IT-UI-011 resolutions after unmount are silent', () => {
  it('does not error or write state when list and detail responses land after unmount', async () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})

    let listPending!: DeferredHandle
    const list = renderIsland((d) => {
      listPending = d.queueDeferred()
    })
    list.unmount()
    await act(async () => {
      listPending.resolve(jsonResponse(200, envelope([fixInq1()])))
    })

    const detail = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await detail.findByRole('row', { name: /O'Neill/ })
    const detailPending = detail.double.queueDeferred()
    await user.click(detail.getByRole('button', { name: /details for Avery O'Neill/i }))
    detail.unmount()
    await act(async () => {
      detailPending.resolve(jsonResponse(200, fixInq1()))
    })

    expect(errorSpy).not.toHaveBeenCalled()
    errorSpy.mockRestore()
  })
})
