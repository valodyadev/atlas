---
name: atlas-handoff
description: Record the state of a change before stopping or switching context - decisions into an ADR, risks and open questions into the open-items log, and what the next session needs to know.
argument-hint: [optional note about what you are stopping in the middle of]
disable-model-invocation: true
---

# Handoff: $ARGUMENTS

Writes to `docs/` only. Never commits.

## 1. Capture the current state

```!
git --no-pager status --short
git --no-pager diff --stat
```

## 2. Route each item to where it belongs

| Item | Goes to |
| --- | --- |
| A decision that is hard to reverse or sets a precedent | a new ADR in `docs/adr/`, from `template.md` |
| A risk that is real but accepted for now | the Risks table in `docs/workflow/open-items.md` |
| A question only the user can answer | the Open questions table in the same file |
| Work deliberately left undone | the Deferred table in the same file |
| A fact a future session would waste time rediscovering | `CLAUDE.md`, or a path-scoped rule in `.claude/rules/` |
| A source consulted for a decision | `docs/workflow/research-sources.md`, with URL and date read |

An ADR is immutable once accepted: supersede, never edit. An ADR with no
downsides listed has not been thought through.

## 3. Write the handoff summary

Short, and in the file — not only in the conversation, which the next session
will not have:

- what is done and verified, with the evidence
- what is done but **not** verified, and why
- what is in progress and where it stopped
- the exact next step

## 4. Leave the tree as the user expects

Do not commit, push, create a branch or initialise git unless the user asks.
Report the uncommitted file count so they know what is pending.
