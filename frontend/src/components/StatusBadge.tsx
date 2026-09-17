import type { StatusName } from '../lib/types'

interface StatusBadgeProps {
  status: StatusName
  colorblind?: boolean
  className?: string
}

/**
 * Material Design 3 Semantic Status Chip.
 * In standard mode: displays contrast-verified semantic background and text.
 * In colorblind mode: pairs color with distinctive M3 SVG glyphs, tactile border
 * patterns (solid, dashed, dotted, double), and geometric corner shapes so status
 * is never communicated through hue alone (WCAG 1.4.1 & M3 Design Principles).
 */
export function StatusBadge({ status, colorblind = false, className = '' }: StatusBadgeProps) {
  const statusLower = status.toLowerCase()
  const combinedClass = [
    'status-badge',
    `status-${statusLower}`,
    colorblind ? 'status-badge--colorblind' : '',
    className,
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <span
      className={combinedClass}
      data-status={status}
      data-colorblind={colorblind ? 'true' : undefined}
    >
      <StatusGlyph status={status} />
      {status}
    </span>
  )
}

function StatusGlyph({ status }: { status: StatusName }) {
  switch (status) {
    case 'New':
      // 4-point sparkle star: unread / fresh intake
      return (
        <svg
          className="status-badge-icon"
          aria-hidden="true"
          viewBox="0 0 16 16"
          width="12"
          height="12"
          fill="currentColor"
        >
          <path d="M8 1L9.6 5.4L14 7L9.6 8.6L8 13L6.4 8.6L2 7L6.4 5.4L8 1Z" />
        </svg>
      )
    case 'Contacted':
      // Conversation chat bubble: communication in-flight
      return (
        <svg
          className="status-badge-icon"
          aria-hidden="true"
          viewBox="0 0 16 16"
          width="12"
          height="12"
          fill="currentColor"
        >
          <path d="M2.5 3C1.67 3 1 3.67 1 4.5V10.5C1 11.33 1.67 12 2.5 12H4.5V14.5L7.5 12H13.5C14.33 12 15 11.33 15 10.5V4.5C15 3.67 14.33 3 13.5 3H2.5ZM3 5.5H13V7H3V5.5ZM3 8.5H10V10H3V8.5Z" />
        </svg>
      )
    case 'Pending':
      // Clock / hourglass: awaiting response or verification
      return (
        <svg
          className="status-badge-icon"
          aria-hidden="true"
          viewBox="0 0 16 16"
          width="12"
          height="12"
          fill="currentColor"
        >
          <path d="M8 1.5C4.41 1.5 1.5 4.41 1.5 8C1.5 11.59 4.41 14.5 8 14.5C11.59 14.5 14.5 11.59 14.5 8C14.5 4.41 11.59 1.5 8 1.5ZM8 13C5.24 13 3 10.76 3 8C3 5.24 5.24 3 8 3C10.76 3 13 5.24 13 8C13 10.76 10.76 13 8 13ZM8.5 4.5H7V8.5L10.5 10.6L11.25 9.35L8.5 7.7V4.5Z" />
        </svg>
      )
    case 'Registered':
      // Verified checkmark: completed intake & course enrollment
      return (
        <svg
          className="status-badge-icon"
          aria-hidden="true"
          viewBox="0 0 16 16"
          width="12"
          height="12"
          fill="currentColor"
        >
          <path d="M6.3 11.7L2.6 8L3.7 6.9L6.3 9.5L12.3 3.5L13.4 4.6L6.3 11.7Z" />
        </svg>
      )
    case 'Closed':
      // Muted circle-slash: archived inquiry
      return (
        <svg
          className="status-badge-icon"
          aria-hidden="true"
          viewBox="0 0 16 16"
          width="12"
          height="12"
          fill="currentColor"
        >
          <path d="M8 1.5C4.41 1.5 1.5 4.41 1.5 8C1.5 11.59 4.41 14.5 8 14.5C11.59 14.5 14.5 11.59 14.5 8C14.5 4.41 11.59 1.5 8 1.5ZM3 8C3 5.24 5.24 3 8 3C9.17 3 10.25 3.41 11.11 4.1L4.1 11.11C3.41 10.25 3 9.17 3 8ZM8 13C6.83 13 5.75 12.59 4.89 11.9L11.9 4.89C12.59 5.75 13 6.83 13 8C13 10.76 10.76 13 8 13Z" />
        </svg>
      )
  }
}
