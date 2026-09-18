import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { LinearProgress, Spinner } from './Progress'

afterEach(cleanup)

describe('Progress indicators', () => {
  it('renders the spinner as decorative by default', () => {
    const { container } = render(<Spinner />)
    const spinner = container.querySelector('.spinner')
    expect(spinner).not.toBeNull()
    expect(spinner).toHaveAttribute('aria-hidden', 'true')
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('promotes the spinner to an announced progressbar when labelled', () => {
    render(<Spinner label="Loading report" />)
    const spinner = screen.getByRole('progressbar', { name: 'Loading report' })
    expect(spinner).not.toHaveAttribute('aria-hidden')
  })

  it('exposes the linear progress as an indeterminate progressbar', () => {
    render(<LinearProgress label="Applying CRM behavior" />)
    const bar = screen.getByRole('progressbar', { name: 'Applying CRM behavior' })
    expect(bar).toBeInTheDocument()
    // Indeterminate: no value is reported.
    expect(bar).not.toHaveAttribute('aria-valuenow')
  })
})
