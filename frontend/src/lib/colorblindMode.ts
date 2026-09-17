/**
 * Material Design 3 Colorblind Mode state and persistence manager.
 * Stores user preference in localStorage and synchronizes with system contrast preferences.
 * Emits custom events on window so decoupled UI regions stay aligned.
 */

export const COLORBLIND_STORAGE_KEY = 'merrithew_colorblind_mode'
export const COLORBLIND_EVENT = 'merrithew:colorblind-change'

/** Detects whether colorblind mode should initially be active. */
export function getInitialColorblindMode(): boolean {
  if (typeof window === 'undefined') {
    return false
  }

  try {
    const stored = window.localStorage.getItem(COLORBLIND_STORAGE_KEY)
    if (stored !== null) {
      return stored === 'true'
    }
  } catch {
    // localStorage may be unavailable in restricted environments or security sandboxes
  }

  // If no explicit preference is set, respect system accessibility preference (prefers-contrast: more)
  try {
    if (window.matchMedia && window.matchMedia('(prefers-contrast: more)').matches) {
      return true
    }
  } catch {
    // matchMedia unavailable in some test runners
  }

  return false
}

/** Persists colorblind mode preference and updates DOM data attributes. */
export function setColorblindModePreference(enabled: boolean): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(COLORBLIND_STORAGE_KEY, String(enabled))
  } catch {
    // Ignore storage write errors
  }

  // Sync data attribute on documentElement for global CSS token activation
  if (document.documentElement) {
    if (enabled) {
      document.documentElement.setAttribute('data-colorblind', 'true')
    } else {
      document.documentElement.removeAttribute('data-colorblind')
    }
  }

  // Dispatch custom event for cross-component and shell synchronization
  try {
    window.dispatchEvent(
      new CustomEvent(COLORBLIND_EVENT, { detail: { enabled } }),
    )
  } catch {
    // Ignore event dispatch failure in non-browser environments
  }
}
