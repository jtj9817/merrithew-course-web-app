interface LiveRegionsProps {
  polite: string
  assertive: string
}

/** The island's single polite and single assertive announcement region (C7). */
export function LiveRegions({ polite, assertive }: LiveRegionsProps) {
  return (
    <div className="live-regions">
      <p role="status" aria-live="polite" className="live-region live-region--polite">
        {polite}
      </p>
      <p role="alert" aria-live="assertive" className="live-region live-region--assertive">
        {assertive}
      </p>
    </div>
  )
}
