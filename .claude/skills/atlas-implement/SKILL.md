---
name: atlas-implement
description: Stage 3 of the Atlas workflow. Route implementation work to the owning agent - backend for backend/ and services/, iOS for ios/ - running them in parallel only when the paths are disjoint.
argument-hint: [the task, ideally referencing an approved plan]
---

# Implement: $ARGUMENTS

## Route by ownership

| Paths | Agent |
| --- | --- |
| `backend/**`, `services/**`, `contracts/**` | **atlas-backend** |
| `ios/**` | **atlas-ios** |
| `docs/adr/**`, `docs/architecture.md` | **atlas-architect** |
| `infra/**`, `.github/**`, `Makefile` | whoever owns the change; say which |

One owner per path, always. If a task needs both sides, split it into two
briefs rather than letting one agent reach across.

## Parallel or serial

Run **atlas-backend** and **atlas-ios** at the same time when, and only when:

- the file sets are disjoint, and
- neither depends on the other's output.

Go serial when the API contract changes: backend regenerates
`contracts/openapi/v1.json` first, then iOS adapts to it. Running those in
parallel means iOS codes against a contract that is being rewritten underneath it.

If true isolation is needed — two agents on overlapping files, or a risky
refactor — give the agent `isolation: "worktree"` on the `Agent` call so it
works on its own copy of the repo. Note that its changes then land on a
separate branch, not in your working tree.

## Brief each agent properly

A subagent starts with a **fresh, isolated context**. It cannot see this
conversation, the plan you just read, or files already opened. Every brief must
carry:

- the goal and the approved approach
- the exact files it owns for this task, and the paths it must not touch
- the gate it must run, and the instruction to paste the real output
- what to hand back for the other side to pick up

## Require evidence

"It builds" is not evidence. Each agent must report the command it ran and the
output. If an agent reports success without that, ask for it before believing it.

## After implementation

Summarise: what changed per area, the gates each agent ran, anything handed off,
and anything that turned out differently from the plan. Then run
`/atlas-verify`.
