# 8. Free-form status transitions with enum validation

Date: 2026-09-16
Status: Accepted

## Context

The spec lists **suggested** statuses (New, Contacted, Pending, Registered,
Closed) and describes staff updating an inquiry "as it progresses" (Part 1). It
does **not** mandate a state machine. In real triage, staff routinely correct
statuses in both directions (e.g. someone marked Registered by mistake goes back
to Pending). `PUT /api/inquiries/{id}/status` is the only status mutation path.

## Decision

Status changes are **free-form**: any **valid** status value is accepted, in any
order. An unknown value is rejected with a 400. The set of valid values is
enforced by binding to an **enum**.

## Options considered

### Free-form + enum validation (chosen)
- Matches how staff actually work; no legitimate correction is blocked.
- Still validates input (garbage values are rejected).
- Simple service logic.

### Enforced progression (state machine) (rejected)
- Only legal transitions allowed (e.g. `Closed → New` rejected with 409).
- Would make a sharp business-rule test, but **blocks legitimate corrections** and
  reads as over-engineering against a spec that calls the statuses "suggested".

## Consequences

**Good:** flexible for staff; minimal code; input is still validated.

**Bad:** no guard against illogical jumps (e.g. `Registered → New`); the "meaning"
of a transition is not enforced.

**Watch for:** if the business later wants guarded transitions, introduce a state
machine in the service and supersede this ADR — the enforced-progression test is
then ready to write. The required automated test instead targets the
default-status/timestamp rule and the CRM-failure-isolation rule, which are the
invariants that actually matter here.
