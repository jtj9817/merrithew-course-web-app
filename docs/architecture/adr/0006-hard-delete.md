# 6. Hard delete for inquiries

Date: 2026-09-16
Status: Accepted

## Context

The spec lets us implement `DELETE /api/inquiries/{id}` as either a hard delete or
a soft delete/archive, and **requires the choice to be justified in the README**
(Part 1, note). The status set already includes **`Closed`**, which covers the
"keep the record but take it out of active work" case.

## Decision

`DELETE` performs a **hard delete**: the row is removed from the database.

## Options considered

### Hard delete (chosen)
- Simplest semantics; no `IsDeleted` flag and no query filter threaded through
  every read.
- The `Closed` status already provides a "retain but deactivate" path, so a
  separate archive concept would be redundant at this scope.

### Soft delete / archive (rejected)
- Preserves data and is reversible — the right default for real lead data.
- Rejected *for this assessment*: it duplicates what `Closed` already expresses,
  and adds a flag plus global query filters for little demonstrable benefit in a
  short exercise.

## Consequences

**Good:** minimal code; list queries stay filter-free; behaviour is obvious.

**Bad:** deletion is **irreversible** and leaves no audit trail.

**Watch for:** in a real deployment, inquiry (lead) data usually warrants
soft-delete plus retention/audit. This is a deliberate scope trade-off and is
listed under "what I'd improve with more time" in the README. If revisited,
supersede this ADR rather than editing it.
