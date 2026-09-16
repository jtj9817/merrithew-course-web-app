# 1. Record architecture decisions

Date: 2026-09-16
Status: Accepted

## Context

This is a technical assessment graded partly on *how the solution is organized and
why* (spec Part 5, "Code quality"). Several choices in this build are deliberate
and would otherwise be invisible in the code — a reviewer would see the outcome
but not the alternatives that were weighed. The planning session on 2026-09-16
made those choices explicitly.

## Decision

We record each significant, costly-to-reverse decision as a short ADR in
`docs/architecture/adr/`, numbered and immutable. Each ADR states the context,
the decision, the options considered (including why the losers lost), and the
consequences we accept — good and bad.

These ADRs are the source for the Part 5 code-quality answer and feed the
`README.md` ("assumptions", "what I'd improve with more time") and
`written-answers.md`.

## Consequences

**Good:** the reasoning survives; the reviewer can see the trade-offs, not just
the result; the written answers have a single source.

**Bad:** a small upkeep cost — a changed decision means a *new* superseding ADR,
not an edit.

**Watch for:** ADRs describe the *target* design agreed in planning. Until the
code exists, they are intent, not fact — see `../model.json` (`mode: greenfield`).
