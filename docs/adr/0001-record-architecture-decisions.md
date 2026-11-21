# 0001. Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

A codebase expected to grow will outlive the memory of why it is shaped the
way it is, and will outlive some of the people who shaped it. The expensive
failure is not a wrong decision — it is a decision nobody can explain, which
therefore nobody dares to change. Six months in, "why is persistence
per-module?" has no answer, so the constraint is either cargo-culted forever
or broken by someone who never knew it was load-bearing.

## Decision

Record every architecturally significant decision as a numbered Markdown file
in `docs/adr/`. Significant means: it is hard to reverse, it constrains how
other code must be written, or it will be questioned by a newcomer.

ADRs are immutable once accepted. A changed decision gets a new ADR that
supersedes the old one.

## Alternatives considered

| Option | Why not |
| --- | --- |
| A wiki | Drifts from the code, is not reviewed, and nobody finds it |
| Comments in code | Good for local "why", useless for cross-cutting decisions |
| Nothing | The default, and the reason most codebases cannot be reasoned about |

## Consequences

**Good**

- A newcomer can read eight files and understand the system's shape.
- Decisions are reviewed in pull requests, alongside the code they constrain.
- Disagreement surfaces at decision time instead of at implementation time.

**Bad / accepted costs**

- Writing them takes time, and a stale ADR is worse than none — hence the
  supersede-don't-edit rule.

**What this makes harder later**

- Nothing, as long as the discipline holds. The failure mode is quiet
  abandonment: ADRs stop at 0008 while the architecture keeps moving.

## Revisit when

Never. If this process stops being used, that is a signal about the team, not
about the process.
