import type { KeyboardEvent } from 'react'

interface ColorblindToggleProps {
  enabled: boolean
  onToggle: () => void
  id?: string
  className?: string
  variant?: 'appbar' | 'standard'
}

/**
 * Material Design 3 Accessible Switch / Filter Chip component.
 * Adheres to M3 principles:
 * - Multimodal state encoding: track color, thumb translation, icon morph, text label.
 * - Minimum 48px touch target for interactive controls.
 * - Native keyboard operability (Space & Enter) and accessible switch semantics.
 * - WCAG AAA contrast ratio in both active and inactive states.
 */
export function ColorblindToggle({
  enabled,
  onToggle,
  id = 'colorblind-toggle',
  className = '',
  variant = 'standard',
}: ColorblindToggleProps) {
  const handleKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault()
      onToggle()
    }
  }

  return (
    <div className={`m3-toggle-wrapper m3-toggle-wrapper--${variant} ${className}`.trim()}>
      <button
        type="button"
        id={id}
        role="switch"
        aria-checked={enabled}
        aria-label="Colorblind mode"
        className={`m3-switch-chip ${enabled ? 'm3-switch-chip--selected' : ''}`}
        onClick={onToggle}
        onKeyDown={handleKeyDown}
      >
        {/* Leading Accessibility Icon */}
        <span className="m3-switch-leading-icon" aria-hidden="true">
          <svg viewBox="0 0 20 20" width="16" height="16" fill="currentColor">
            <path d="M10 2C5.58 2 2 5.58 2 10C2 14.42 5.58 18 10 18C14.42 18 18 14.42 18 10C18 5.58 14.42 2 10 2ZM10 16.2C6.58 16.2 3.8 13.42 3.8 10C3.8 6.58 6.58 3.8 10 3.8C13.42 3.8 16.2 6.58 16.2 10C16.2 13.42 13.42 16.2 10 16.2ZM10 5V15C12.76 15 15 12.76 15 10C15 7.24 12.76 5 10 5Z" />
          </svg>
        </span>

        {/* Text Label */}
        <span className="m3-switch-label">
          Colorblind Mode
        </span>

        {/* M3 Switch Track & Handle */}
        <span className="m3-switch-track" aria-hidden="true">
          <span className="m3-switch-thumb">
            {enabled ? (
              <svg className="m3-thumb-icon" viewBox="0 0 16 16" width="10" height="10" fill="currentColor">
                <path d="M6.3 11.7L2.6 8L3.7 6.9L6.3 9.5L12.3 3.5L13.4 4.6L6.3 11.7Z" />
              </svg>
            ) : (
              <svg className="m3-thumb-icon" viewBox="0 0 16 16" width="8" height="8" fill="currentColor">
                <circle cx="8" cy="8" r="4" />
              </svg>
            )}
          </span>
        </span>
      </button>
    </div>
  )
}
