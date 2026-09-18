import type { ReactNode } from 'react'
import { crmSimulationToolsEnabled } from '../lib/crmSimulation'
import { intakeFaultToolsEnabled } from '../lib/intakeFault'
import { reconciliationToolsEnabled } from '../lib/reconciliation'
import { scenarioToolsEnabled } from '../lib/scenarios'

interface DevToolsZoneProps {
  /** The dev-tool panels; each still gates itself on its own shell flag. */
  children: ReactNode
}

/**
 * Wraps the dev-only panels in one labeled section so the whole block reads as
 * a single scenario harness instead of four ad-hoc widgets. The shell sets each
 * panel's window flag independently, so the header renders when any flag is on;
 * with every flag off the section stays empty and CSS hides it via :empty.
 * Keep this container free of stray text nodes: :empty is what hides the zone
 * in production when every panel renders null.
 */
export function DevToolsZone({ children }: DevToolsZoneProps) {
  const enabled =
    scenarioToolsEnabled() ||
    crmSimulationToolsEnabled() ||
    intakeFaultToolsEnabled() ||
    reconciliationToolsEnabled()

  return (
    <section className="dev-tools" aria-label="Developer tools">
      {enabled && (
        <header className="dev-tools-header">
          <span className="dev-tools-tag">DEV</span>
          <p className="dev-tools-lede">
            Seed demo data, simulate CRM delivery, inject intake faults, and reconcile stored
            counts.
          </p>
        </header>
      )}
      {children}
    </section>
  )
}
