---
name: atlas-plan
description: Stage 1 of the Atlas workflow. Research and plan a change before any code is written - architecture lead, boundary check, work split per agent, and an ADR draft when the decision is significant.
argument-hint: [what you want to build or change]
---

# Plan: $ARGUMENTS

Do not write implementation code in this stage. The output is a plan.

## 1. Orient (do this yourself, briefly)

Read `docs/architecture.md` and skim `docs/adr/README.md`. Identify which areas
the work touches: `backend/`, `ios/`, `services/`, `infra/`, `contracts/`.

## 2. Delegate the architecture call

Use the **atlas-architect** agent. Its brief must be self-contained — a
subagent starts with a fresh context and cannot see this conversation. Include:

- the goal, in the user's words
- which areas you believe it touches, and why
- any constraint the user stated
- the explicit question: does this respect the five rules in
  `docs/architecture.md`, and is an ADR needed?

Ask it to research anything that turns on information outside the repo
(a library's current state, a version's support window) with `WebSearch` /
`WebFetch` and to cite URL plus date read.

## 3. Get a test plan in parallel

Once the architect's shape is known, use **atlas-qa** to say what would prove
the change works, at the cheapest tier that can prove it. This runs in parallel
with nothing else writing files, so it is safe alongside step 2's follow-ups.

## 4. Produce the plan

Present, in this order:

1. **Approach** — one paragraph, and the alternative you rejected.
2. **Work split** — a table of agent, files it will own for this change, and
   what done looks like. No two agents may own the same path.
3. **Sequencing** — what must be serial and what may be parallel. If the change
   alters `contracts/openapi/v1.json`, backend goes first and iOS follows; they
   cannot overlap.
4. **The check** — the exact command that will prove it works.
5. **Risks and open questions** — anything unresolved.
6. **ADR** — a draft from `docs/adr/template.md` if the decision is hard to
   reverse, constrains other code, or a newcomer would question it.

Then stop and let the user approve before implementing. Append risks and open
questions to `docs/workflow/open-items.md`.
