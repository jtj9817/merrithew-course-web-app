/**
 * Material Design 3 progress indicators used by the dev-tool controls to show
 * in-flight work. Both are indeterminate: the duration of a scenario run is not
 * known ahead of time, so neither carries aria-valuenow.
 *
 * The Spinner is decorative by default (aria-hidden) because it sits beside a
 * text label that a live region already announces; pass a `label` only when the
 * spinner is the sole indicator. The LinearProgress is the announced one: it
 * exposes role="progressbar" with the action name.
 */

interface SpinnerProps {
  /** Diameter in pixels. Defaults to a button-sized 18px. */
  size?: number
  /** When set, the spinner is the announced indicator (role="progressbar"). */
  label?: string
  className?: string
}

export function Spinner({ size = 18, label, className }: SpinnerProps) {
  return (
    <span
      className={className ? `spinner ${className}` : 'spinner'}
      style={{ width: size, height: size }}
      role={label ? 'progressbar' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    />
  )
}

interface LinearProgressProps {
  /** Names the running action for assistive tech, e.g. "Applying CRM behavior". */
  label: string
  className?: string
}

export function LinearProgress({ label, className }: LinearProgressProps) {
  return (
    <div
      className={className ? `linear-progress ${className}` : 'linear-progress'}
      role="progressbar"
      aria-label={label}
    >
      <div className="linear-progress-track">
        <div className="linear-progress-indicator" />
      </div>
    </div>
  )
}
