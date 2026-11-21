---
name: atlas-review
description: Stage 5 of the Atlas workflow. Review the current change for correctness, coverage and boundary violations, skipping style. Runs QA and architecture review in parallel because both are read-only on implementation paths.
argument-hint: [optional focus, e.g. the migration, or the pagination change]
---

# Review: $ARGUMENTS

Start from the actual diff:

```!
git --no-pager diff --stat
```

Run both reviews **in parallel** — they are read-only on implementation paths,
so they cannot conflict:

## atlas-qa — correctness and coverage

- Concrete failure scenarios only: inputs and state leading to a wrong result.
- Ranked by consequence. A silent wrong answer outranks an obvious crash.
- Is there a test that fails if this change is reverted?
- Migration safety: backward compatible with the deployed version? Both
  versions serve traffic during a rolling deploy.
- **Skip style.** Formatters and analysers own it.

## atlas-architect — boundaries and precedent

- Do the five rules in `docs/architecture.md` still hold?
- Was `Atlas.Architecture.Tests` weakened to make something pass? That is a
  blocking finding.
- Does this contradict an accepted ADR? Then it needs a superseding ADR, not a
  quiet exception.
- Does it set a precedent that should be recorded?

## Note

The repo also has a built-in `/code-review` command, which runs its own
multi-agent review of the diff. Use that for a broad correctness sweep; use
this skill when you want the Atlas-specific boundary and coverage judgement
alongside it.

## Report

One merged list, ordered by severity, each finding with file, line and a
concrete scenario. Then: ship / fix first / needs a decision. If the change is
sound, say so plainly — a reviewer asked for problems will always invent some.
