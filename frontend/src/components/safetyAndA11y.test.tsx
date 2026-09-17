import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { htmlResponse, jsonResponse } from '../test/fetchDouble'
import {
  envelope,
  fixInq1,
  fixInq1Contacted,
  fixInq2,
  fixInqXss,
  problem500Dirty,
} from '../test/fixtures'
import { renderIsland, uninstallActiveIsland } from '../test/islandTestKit'

afterEach(uninstallActiveIsland)

const user = userEvent.setup()

function islandRoot(): HTMLElement {
  return document.querySelector('.island') as HTMLElement
}

describe('IT-UI-024 visitor text renders inertly', () => {
  beforeEach(() => {
    delete (window as { __xss?: boolean }).__xss
  })

  it('keeps markup as text and Unicode verbatim in table and detail', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInqXss(), fixInq2()]))),
    )
    const xssRow = await screen.findByRole('row', { name: /Ada/ })

    expect((window as { __xss?: boolean }).__xss).toBeUndefined()
    expect(xssRow.querySelector('img, script')).toBeNull()
    expect(islandRoot().querySelector('img, script')).toBeNull()
    expect(xssRow.textContent).toContain('<img src=x')
    expect(screen.getByText(/Céline/)).toBeInTheDocument()
    expect(screen.getByText(/Fernández/)).toBeInTheDocument()

    double.queueResponse(jsonResponse(200, fixInqXss()))
    await user.click(
      within(xssRow).getByRole('button', { name: /details for Ada/i }),
    )
    await screen.findByText('Email')

    expect((window as { __xss?: boolean }).__xss).toBeUndefined()
    expect(islandRoot().querySelector('img, script')).toBeNull()
    const panel = document.querySelector('.detail-panel') as HTMLElement
    expect(panel.textContent).toContain('<img src=x')
    expect(panel.textContent).toContain('<script>window.__xss=true</script>')
    expect(panel.textContent).toContain('"quotes"')
    expect(panel.textContent).toContain("'apostrophes'")
    expect(panel.textContent).toContain('&')
  })
})

describe('IT-UI-025 non-JSON and wrong-shape failures degrade safely', () => {
  it('renders a generic recoverable message for a 502 HTML list response', async () => {
    renderIsland((d) =>
      d.queueResponse(htmlResponse(502, '<html><body>boom</body></html>')),
    )

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/could not be loaded|went wrong/i)
    expect(screen.queryByText(/boom/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /retry/i })).toBeEnabled()
  })

  it('renders a generic recoverable message for a wrong-shape 500 update response', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(500, { foo: 1 }))
    await user.selectOptions(
      within(oneill).getByRole('combobox', { name: "Status for Avery O'Neill" }),
      'Contacted',
    )
    await user.click(within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/could not be saved|try again/i)
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
    expect(screen.getByRole('row', { name: /O'Neill/ })).toBeInTheDocument()
    await waitFor(() =>
      expect(
        within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" }),
      ).toBeEnabled(),
    )
  })
})

describe('IT-UI-026 unsafe ProblemDetails fields never reach the DOM', () => {
  it('renders only the safe title from a dirty 500 and stays interactive', async () => {
    renderIsland((d) => d.queueResponse(jsonResponse(500, problem500Dirty())))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/unexpected error/i)
    expect(document.body.textContent).not.toContain('SELECT * FROM CourseInquiries')
    expect(document.body.textContent).not.toContain('Server=tcp:test')
    expect(screen.getByRole('button', { name: /retry/i })).toBeEnabled()
  })
})

describe('IT-UI-027 controls are labeled and keyboard-reachable in DOM order', () => {
  it('labels every control and reaches each action by Tab, opening detail with Enter', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    screen.getByLabelText('Filter by status')
    screen.getByLabelText('Sort order')
    screen.getByRole('combobox', { name: "Status for Avery O'Neill" })
    screen.getByRole('button', { name: "Apply for Avery O'Neill" })

    const stops: string[] = []
    for (let index = 0; index < 5; index += 1) {
      await user.tab()
      stops.push(
        document.activeElement?.getAttribute('aria-label') ??
          document.activeElement?.id ??
          document.activeElement?.tagName ??
          '',
      )
    }
    expect(stops).toEqual([
      'status-filter',
      'sort-order',
      "Details for Avery O'Neill",
      "Status for Avery O'Neill",
      "Apply for Avery O'Neill",
    ])

    // Walk back to the row opener with the keyboard and activate it with Enter.
    await user.tab({ shift: true })
    await user.tab({ shift: true })
    expect(document.activeElement?.getAttribute('aria-label')).toBe("Details for Avery O'Neill")

    double.queueResponse(jsonResponse(200, fixInq1()))
    await user.keyboard('{Enter}')
    await screen.findByText('Email')
    expect(document.querySelector('.detail-panel')).not.toBeNull()
  })
})

describe('IT-UI-028 detail focus moves in and returns to the opener', () => {
  it('focuses the panel on open and restores the opener on Escape and Close', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })
    const opener = within(oneill).getByRole('button', { name: /details for Avery O'Neill/i })

    double.queueResponse(jsonResponse(200, fixInq1()))
    await user.click(opener)
    await screen.findByText('Email')
    const panel = document.querySelector('.detail-panel') as HTMLElement
    expect(panel).toHaveFocus()
    expect(document.activeElement).not.toBe(document.body)

    await user.keyboard('{Escape}')
    expect(document.querySelector('.detail-panel')).toBeNull()
    expect(opener).toHaveFocus()

    double.queueResponse(jsonResponse(200, fixInq1()))
    await user.click(opener)
    await screen.findByText('Email')
    await user.click(screen.getByRole('button', { name: 'Close' }))
    expect(document.querySelector('.detail-panel')).toBeNull()
    expect(opener).toHaveFocus()
  })
})

describe('IT-UI-029 success and failure announce in their dedicated regions', () => {
  it('routes saved to the status region and failure to the alert region', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    const oneill = await screen.findByRole('row', { name: /O'Neill/ })
    const select = within(oneill).getByRole('combobox', {
      name: "Status for Avery O'Neill",
    })
    const apply = within(oneill).getByRole('button', { name: "Apply for Avery O'Neill" })

    double.queueResponse(jsonResponse(200, fixInq1Contacted()))
    double.queueResponse(jsonResponse(200, envelope([fixInq1Contacted(), fixInq2()])))
    await user.selectOptions(select, 'Contacted')
    await user.click(apply)

    expect(await screen.findByRole('status')).toHaveTextContent(/saved/i)
    expect(screen.getByRole('alert')).toBeEmptyDOMElement()
    await waitFor(() =>
      expect(
        within(screen.getByRole('row', { name: /O'Neill/ })).getByText('Contacted', {
          selector: '.status-badge',
        }),
      ).toBeInTheDocument(),
    )

    double.queueRejection(new TypeError('Failed to fetch'))
    await user.selectOptions(select, 'Pending')
    await user.click(apply)

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be saved|try again/i)
    expect(screen.getByRole('status')).toHaveTextContent(/saved/i)
  })
})

describe('IT-UI-030 the island grows no create or delete affordances', () => {
  it('exposes no create/delete controls anywhere in the island', async () => {
    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    double.queueResponse(jsonResponse(200, fixInq1()))
    await user.click(screen.getByRole('button', { name: /details for Avery O'Neill/i }))
    await screen.findByText('Email')

    const forbidden = /create|add inquiry|delete|remove/i
    const buttons = screen
      .getAllByRole('button')
      .filter((button) => forbidden.test(button.textContent ?? ''))
    expect(buttons).toEqual([])
    const namedControls = [islandRoot(), document.querySelector('.detail-panel') as HTMLElement]
      .flatMap((root) => Array.from(root.querySelectorAll('button, select, input, a')))
      .filter((control) => forbidden.test(control.getAttribute('aria-label') ?? ''))
    expect(namedControls).toEqual([])
  })
})
