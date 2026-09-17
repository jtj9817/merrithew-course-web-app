import { describe, expect, it } from 'vitest'
import { applyFilterChange, applyPageChange } from './listState'
import type { ListUiState } from './listState'

describe('UT-UI-003 filter changes reset the page, page changes preserve the filter', () => {
  it('resets the page to 1 when the filter changes', () => {
    const state: ListUiState = { filter: 'New', page: 3 }
    expect(applyFilterChange(state, 'Closed')).toEqual({ filter: 'Closed', page: 1 })
  })

  it('preserves the filter when the page changes', () => {
    const state = applyFilterChange({ filter: 'New', page: 3 }, 'Closed')
    expect(applyPageChange(state, 2)).toEqual({ filter: 'Closed', page: 2 })
  })
})
