import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { COLORBLIND_STORAGE_KEY } from '../lib/colorblindMode'
import { jsonResponse } from '../test/fetchDouble'
import { envelope, fixInq1, fixInq2 } from '../test/fixtures'
import { renderIsland, uninstallActiveIsland } from '../test/islandTestKit'

let shellAppbar: HTMLElement | null = null

/** Mounts the Razor shell appbar badge, which hosts the colorblind toggle portal target. */
function mountShellAppbar(): void {
  const badge = document.createElement('div')
  badge.className = 'shell-appbar-badge'
  const target = document.createElement('div')
  target.id = 'shell-appbar-colorblind'
  badge.appendChild(target)
  document.body.appendChild(badge)
  shellAppbar = badge
}

afterEach(() => {
  uninstallActiveIsland()
  window.localStorage.clear()
  document.documentElement.removeAttribute('data-colorblind')
  shellAppbar?.remove()
  shellAppbar = null
})

const user = userEvent.setup()

describe('Material Design 3 Colorblind Mode', () => {
  beforeEach(() => {
    window.localStorage.clear()
    document.documentElement.removeAttribute('data-colorblind')
    mountShellAppbar()
  })

  it('initializes in disabled state by default with proper M3 switch semantics', async () => {
    renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    const toggle = screen.getByRole('switch', { name: /colorblind mode/i })
    expect(toggle).toBeInTheDocument()
    expect(toggle).toHaveAttribute('aria-checked', 'false')
    expect(document.documentElement.getAttribute('data-colorblind')).toBeNull()
  })

  it('respects prefers-contrast: more when no explicit localStorage preference is set', async () => {
    const originalMatchMedia = window.matchMedia
    window.matchMedia = vi.fn().mockImplementation((query: string) => ({
      matches: query === '(prefers-contrast: more)',
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }))

    try {
      renderIsland((d) =>
        d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
      )
      await screen.findByRole('row', { name: /O'Neill/ })

      const toggle = screen.getByRole('switch', { name: /colorblind mode/i })
      expect(toggle).toHaveAttribute('aria-checked', 'true')
      expect(document.documentElement.getAttribute('data-colorblind')).toBe('true')
    } finally {
      window.matchMedia = originalMatchMedia
    }
  })

  it('activates via click, updates localStorage, sets data attribute, and displays M3 glyphs', async () => {
    renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1(), fixInq2()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    const toggle = screen.getByRole('switch', { name: /colorblind mode/i })
    await user.click(toggle)

    // Verify M3 switch state
    expect(toggle).toHaveAttribute('aria-checked', 'true')
    expect(window.localStorage.getItem(COLORBLIND_STORAGE_KEY)).toBe('true')
    expect(document.documentElement.getAttribute('data-colorblind')).toBe('true')

    // Verify polite live region announcement (M3 accessible feedback)
    await waitFor(() => {
      const politeRegion = document.querySelector('[role="status"]')
      expect(politeRegion).toHaveTextContent(/colorblind mode enabled/i)
    })

    // Verify status badges acquire colorblind class and semantic M3 SVG glyphs
    const row1 = screen.getByRole('row', { name: /O'Neill/ })
    const badge1 = within(row1).getByText('New', { selector: '.status-badge' })
    expect(badge1).toHaveClass('status-badge--colorblind')
    expect(badge1).toHaveAttribute('data-colorblind', 'true')

    // SVG icon is present inside the badge
    const icon1 = badge1.querySelector('svg.status-badge-icon')
    expect(icon1).toBeInTheDocument()
    expect(icon1).toHaveAttribute('aria-hidden', 'true')
  })

  it('toggles off and restores default appearance', async () => {
    window.localStorage.setItem(COLORBLIND_STORAGE_KEY, 'true')

    renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    const toggle = screen.getByRole('switch', { name: /colorblind mode/i })
    expect(toggle).toHaveAttribute('aria-checked', 'true')

    // Click to turn off
    await user.click(toggle)

    expect(toggle).toHaveAttribute('aria-checked', 'false')
    expect(window.localStorage.getItem(COLORBLIND_STORAGE_KEY)).toBe('false')
    expect(document.documentElement.getAttribute('data-colorblind')).toBeNull()

    await waitFor(() => {
      const politeRegion = document.querySelector('[role="status"]')
      expect(politeRegion).toHaveTextContent(/colorblind mode disabled/i)
    })

    const row = screen.getByRole('row', { name: /O'Neill/ })
    const badge = within(row).getByText('New', { selector: '.status-badge' })
    expect(badge).not.toHaveClass('status-badge--colorblind')
  })

  it('supports keyboard operation via Space and Enter keys', async () => {
    renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    const toggle = screen.getByRole('switch', { name: /colorblind mode/i })
    toggle.focus()
    expect(toggle).toHaveFocus()

    // Toggle on with Space
    await user.keyboard(' ')
    expect(toggle).toHaveAttribute('aria-checked', 'true')
    expect(document.documentElement.getAttribute('data-colorblind')).toBe('true')

    // Toggle off with Enter
    await user.keyboard('{Enter}')
    expect(toggle).toHaveAttribute('aria-checked', 'false')
    expect(document.documentElement.getAttribute('data-colorblind')).toBeNull()
  })

  it('renders multimodal status badge inside DetailPanel modal when colorblind mode is active', async () => {
    window.localStorage.setItem(COLORBLIND_STORAGE_KEY, 'true')

    const { double } = renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    // Open detail panel
    double.queueResponse(jsonResponse(200, fixInq1()))
    const detailsButton = screen.getByRole('button', { name: /Details for Avery O'Neill/i })
    await user.click(detailsButton)

    await screen.findByText('Email')

    const modal = document.querySelector('.detail-panel') as HTMLElement
    expect(modal).not.toBeNull()
    const modalBadge = within(modal).getByText('New', { selector: '.status-badge' })
    expect(modalBadge).toHaveClass('status-badge--colorblind')
    expect(modalBadge.querySelector('svg.status-badge-icon')).toBeInTheDocument()
  })

  it('renders exactly one colorblind toggle, portaled into the shell appbar badge', async () => {
    renderIsland((d) =>
      d.queueResponse(jsonResponse(200, envelope([fixInq1()]))),
    )
    await screen.findByRole('row', { name: /O'Neill/ })

    const switches = screen.getAllByRole('switch', { name: /colorblind mode/i })
    expect(switches).toHaveLength(1)
    expect(switches[0]).toHaveAttribute('id', 'colorblind-toggle-appbar')
    expect(shellAppbar?.contains(switches[0])).toBe(true)

    await user.click(switches[0])
    expect(switches[0]).toHaveAttribute('aria-checked', 'true')
    expect(document.documentElement.getAttribute('data-colorblind')).toBe('true')
  })
})
