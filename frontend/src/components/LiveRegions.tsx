import { useEffect, useRef, useState } from 'react'

export interface ToastNotification {
  id: number
  variant: 'info' | 'error'
}

interface LiveRegionsProps {
  polite: string
  assertive: string
  toast?: ToastNotification | null
  onDismissToast?: () => void
}

/**
 * Single polite and assertive live region pair (C7) with M3 snackbar presentation.
 * Never steals focus; announces updates through the existing polite/assertive regions.
 * Features 4.5s auto-dismiss with hover/focus pause, queue replacement, and dismiss action.
 */
export function LiveRegions({
  polite,
  assertive,
  toast,
  onDismissToast,
}: LiveRegionsProps) {
  const [isPaused, setIsPaused] = useState(false)
  const remainingRef = useRef(4500)
  const startRef = useRef(Date.now())

  // Queue semantics: a new toast replaces/extends the timer to 4.5s
  useEffect(() => {
    if (!toast) return
    remainingRef.current = 4500
    startRef.current = Date.now()
    setIsPaused(false)
  }, [toast?.id])

  // Auto-dismiss countdown with hover and focus pause
  useEffect(() => {
    if (!toast || isPaused || !onDismissToast) return

    startRef.current = Date.now()
    const timer = setTimeout(() => {
      onDismissToast()
    }, remainingRef.current)

    return () => {
      clearTimeout(timer)
      remainingRef.current = Math.max(0, remainingRef.current - (Date.now() - startRef.current))
    }
  }, [toast, isPaused, onDismissToast])
  return (
    <div className="live-regions">
      {toast?.variant === 'info' && polite ? (
        <div
          className="snackbar snackbar--info"
          onMouseEnter={() => setIsPaused(true)}
          onMouseLeave={() => setIsPaused(false)}
          onPointerEnter={() => setIsPaused(true)}
          onPointerLeave={() => setIsPaused(false)}
          onFocus={() => setIsPaused(true)}
          onBlur={(event) => {
            if (!event.currentTarget.contains(event.relatedTarget)) {
              setIsPaused(false)
            }
          }}
        >
          <p role="status" aria-live="polite" className="snackbar-message">
            {polite}
          </p>
          {onDismissToast && (
            <button
              type="button"
              className="snackbar-action"
              onClick={onDismissToast}
              aria-label="Dismiss notification"
            >
              Dismiss
            </button>
          )}
        </div>
      ) : (
        <p role="status" aria-live="polite" className="sr-only">
          {polite}
        </p>
      )}

      {toast?.variant === 'error' && assertive ? (
        <div
          className="snackbar snackbar--error"
          onMouseEnter={() => setIsPaused(true)}
          onMouseLeave={() => setIsPaused(false)}
          onPointerEnter={() => setIsPaused(true)}
          onPointerLeave={() => setIsPaused(false)}
          onFocus={() => setIsPaused(true)}
          onBlur={(event) => {
            if (!event.currentTarget.contains(event.relatedTarget)) {
              setIsPaused(false)
            }
          }}
        >
          <p role="alert" aria-live="assertive" className="snackbar-message">
            {assertive}
          </p>
          {onDismissToast && (
            <button
              type="button"
              className="snackbar-action"
              onClick={onDismissToast}
              aria-label="Dismiss alert"
            >
              Dismiss
            </button>
          )}
        </div>
      ) : (
        <p role="alert" aria-live="assertive" className="sr-only">
          {assertive}
        </p>
      )}
    </div>
  )
}
