import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { jsonResponse } from '../test/fetchDouble'
import { envelope, fixInq1, fixInq2 } from '../test/fixtures'
import { renderIsland, uninstallActiveIsland } from '../test/islandTestKit'

afterEach(uninstallActiveIsland)

const user = userEvent.setup()

describe('IT-UI-035 a filtered view shows its subset, total, and filter visibly', () => {
  it('renders the indicator above the table only while a filter is active', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()], { totalCount: 2 }))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    // The default All view carries no filtered-state indicator.
    expect(screen.queryByText(/filtered by/i)).not.toBeInTheDocument()

    double.queueResponse(jsonResponse(200, envelope([fixInq1()], { totalCount: 5 })))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'New')

    const section = document.getElementById('inquiry-queue')
    expect(section).not.toBeNull()
    const indicator = await within(section as HTMLElement).findByText(
      'Showing 1 of 5 inquiries · filtered by New',
    )
    expect(
      indicator.compareDocumentPosition(screen.getByRole('table')) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    expect(
      within(section as HTMLElement).getByRole('button', { name: 'Clear filter' }),
    ).toBeInTheDocument()
  })
})

describe('IT-UI-036 Clear filter restores the unfiltered view and the indicator goes away', () => {
  it('refetches without the status parameter and hides the indicator', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()], { totalCount: 2 }))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, envelope([fixInq1()], { totalCount: 5 })))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'New')
    expect(await screen.findByText('Showing 1 of 5 inquiries · filtered by New')).toBeInTheDocument()

    double.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()], { totalCount: 2 })))
    await user.click(screen.getByRole('button', { name: 'Clear filter' }))

    await screen.findByText(/2 inquiries/, { selector: '.total-count' })
    // The live region still announces "Filtered by All"; only the visible
    // indicator must be gone.
    expect(screen.queryByText(/filtered by/i, { selector: '.filtered-state-text' }))
      .not.toBeInTheDocument()
    await waitFor(() => expect(double.calls.at(-1)?.url).not.toContain('status'))
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries?page=1')
  })
})

describe('IT-UI-037 an empty filtered result keeps the indicator visible', () => {
  it('shows zero counts so an empty filter cannot read as missing data', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()], { totalCount: 2 }))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, envelope([], { totalCount: 0 })))
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Closed')

    expect(
      await screen.findByText('Showing 0 of 0 inquiries · filtered by Closed'),
    ).toBeInTheDocument()
    expect(await screen.findByText(/no inquiries match this filter/i)).toBeInTheDocument()
  })
})
