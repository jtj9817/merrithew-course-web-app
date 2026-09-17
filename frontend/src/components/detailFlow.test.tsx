import { act, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { jsonResponse } from '../test/fetchDouble'
import { envelope, fixInq1, fixInq2, problem404 } from '../test/fixtures'
import { renderIsland, uninstallActiveIsland } from '../test/islandTestKit'

afterEach(uninstallActiveIsland)

const user = userEvent.setup()

function panel(): HTMLElement {
  const panelElement = document.querySelector('.detail-panel')
  expect(panelElement).not.toBeNull()
  return panelElement as HTMLElement
}

describe('IT-UI-012 detail panel renders every field with explicit empty markers', () => {
  it('fetches the record and shows all eleven fields, focusing the panel', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, fixInq1()))
    await user.click(screen.getByRole('button', { name: /details for Avery O'Neill/i }))

    await waitForPanelFields()

    expect(double.calls.at(-1)?.url).toBe('/api/inquiries/1')
    const detail = within(panel())
    expect(detail.getByText('1')).toBeInTheDocument()
    expect(detail.getByText('Avery')).toBeInTheDocument()
    expect(detail.getByText("O'Neill")).toBeInTheDocument()
    expect(detail.getByText('avery.oneill@example.com')).toBeInTheDocument()
    expect(detail.getByText('+1 555 0100')).toBeInTheDocument()
    expect(detail.getByText('Yoga Teacher Training — Fall Cohort')).toBeInTheDocument()
    expect(detail.getByText('Calgary NW')).toBeInTheDocument()
    expect(detail.getByText('Please send the syllabus & pricing.')).toBeInTheDocument()
    expect(detail.getByText('New')).toBeInTheDocument()
    expect(detail.getAllByText('2026-09-10').length).toBeGreaterThanOrEqual(2)
    expect(panel()).toHaveFocus()
  })

  it('renders nullable fields as explicit markers, never the string null', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /Fernández/ })

    double.queueResponse(jsonResponse(200, fixInq2()))
    await user.click(screen.getByRole('button', { name: /details for Céline Fernández/i }))

    await waitForPanelFields()

    const detail = within(panel())
    expect(detail.getByText('Céline')).toBeInTheDocument()
    expect(detail.getByText('Fernández')).toBeInTheDocument()
    expect(detail.getByText('Barre Fundamentals')).toBeInTheDocument()
    expect(detail.getAllByText('—').length).toBe(3)
    expect(panel().textContent).not.toContain('null')
  })
})

describe('IT-UI-013 stale detail responses never overwrite a newer selection', () => {
  it('keeps the newer record when an older detail response resolves late', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    const older = double.queueDeferred()
    await user.click(screen.getByRole('button', { name: /details for Avery O'Neill/i }))
    const newer = double.queueDeferred()
    await user.click(screen.getByRole('button', { name: /details for Céline Fernández/i }))

    await act(async () => {
      newer.resolve(jsonResponse(200, fixInq2()))
    })
    await waitForPanelFields()
    expect(within(panel()).getByText('Fernández')).toBeInTheDocument()

    await act(async () => {
      older.resolve(jsonResponse(200, fixInq1()))
    })

    expect(within(panel()).getByText('Fernández')).toBeInTheDocument()
    expect(within(panel()).queryByText('Avery')).not.toBeInTheDocument()
  })
})

describe('IT-UI-014 detail 404 announces gone, clears the panel, and refreshes the list', () => {
  it('explains the record is gone and reconciles from a fresh list', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(404, problem404()))
    double.queueResponse(jsonResponse(200, envelope([fixInq2()])))
    await user.click(screen.getByRole('button', { name: /details for Avery O'Neill/i }))

    const goneMessage = await screen.findByText(/no longer exists|not found|gone/i)
    expect(screen.getByRole('alert')).toContainElement(goneMessage)

    await screen.findByRole('row', { name: /Fernández/ })
    expect(screen.queryByRole('row', { name: /O'Neill/ })).not.toBeInTheDocument()
    expect(document.querySelector('.detail-panel')).toBeNull()
    expect(double.calls.at(-1)?.url).toBe('/api/inquiries')
  })
})

/** The panel's dt "Email" only exists once the record has loaded. */
async function waitForPanelFields(): Promise<void> {
  await screen.findByText('Email')
}
