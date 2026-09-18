import { useState } from 'react'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { CrmSyncLogModal } from './CrmSyncLogModal'
import type { CrmSyncRun } from './CrmSyncLogModal'

// The dialog flow is covered through the CRM control elsewhere; these cases pin
// the focus-recapture path for the state the container handler cannot see:
// focus sitting outside the dialog (Firefox focuses body after clicking
// non-interactive log text), from which Escape and Tab would otherwise leak
// into the background while the modal stays open.
const demoRun: CrmSyncRun = {
  result: { inquiryId: 41, mode: 'TransientThenSuccess', outcome: 'Success', attempts: 3 },
  settings: { mode: 'TransientThenSuccess', transientFailuresBeforeSuccess: 2, latencyMilliseconds: 100 },
}

function Harness() {
  const [run, setRun] = useState<CrmSyncRun | null>(null)
  return (
    <>
      <button type="button" onClick={() => setRun(demoRun)}>
        Open sync log
      </button>
      <CrmSyncLogModal run={run} onClose={() => setRun(null)} />
    </>
  )
}

describe('CrmSyncLogModal focus recapture', () => {
  afterEach(() => {
    cleanup()
  })

  async function openDialogWithEscapedFocus() {
    render(<Harness />)
    const opener = screen.getByRole('button', { name: 'Open sync log' })
    await userEvent.click(opener)
    expect(screen.getByRole('dialog', { name: /Sync log/ })).toHaveFocus()

    // jsdom cannot focus body like Firefox does after a click on non-interactive
    // text, so park focus on a background control: the same escaped-focus state
    // from which the dialog's own keydown handler never fires.
    opener.focus()
    expect(document.activeElement).toBe(opener)
  }

  it('closes on Escape after focus escaped the dialog', async () => {
    await openDialogWithEscapedFocus()

    await userEvent.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('pulls focus back into the dialog on Tab after focus escaped', async () => {
    await openDialogWithEscapedFocus()

    await userEvent.keyboard('{Tab}')

    expect(screen.getByRole('button', { name: 'Close' })).toHaveFocus()
    expect(screen.getByRole('dialog', { name: /Sync log/ })).toBeInTheDocument()
  })
})
