import { render } from '@testing-library/react'
import App from '../App'
import { installFetchDouble } from './fetchDouble'
import type { FetchDouble } from './fetchDouble'

let active: FetchDouble | null = null

/**
 * Installs the fetch double, lets `prepare` program it, and then mounts the
 * island — the initial list request fires during render, so reactions must be
 * queued before mounting.
 */
export function renderIsland(prepare?: (double: FetchDouble) => void): ReturnType<typeof render> & {
  double: FetchDouble
} {
  const double = installFetchDouble()
  active = double
  prepare?.(double)
  return { double, ...render(<App />) }
}

/** Uninstalls the fetch double mounted by <see cref="renderIsland"/>. */
export function uninstallActiveIsland(): void {
  active?.uninstall()
  active = null
}
