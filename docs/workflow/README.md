# The Atlas agentic workflow

Four specialised agents, five workflow stages, one owner per path. This page is
what you read day to day.

Grounded in Anthropic's own guidance rather than assumption — sources and dates
in [`research-sources.md`](research-sources.md).

---

## The agents

| Agent | Leads | Owns (writes) | Tools |
| --- | --- | --- | --- |
| `atlas-architect` | Planning, decisions | `docs/adr/**`, `docs/architecture.md` | Read, Grep, Glob, Bash, Write, Edit, WebSearch, WebFetch |
| `atlas-backend` | Server implementation | `backend/**`, `services/**`, `contracts/**` | Read, Grep, Glob, Bash, Write, Edit |
| `atlas-ios` | App implementation | `ios/**` | Read, Grep, Glob, Bash, Write, Edit |
| `atlas-qa` | Test planning, verification, review | nothing | Read, Grep, Glob, Bash |

`atlas-qa` has no `Write` and no `Edit`. That boundary is enforced by Claude
Code, not by convention: a reviewer that can quietly fix what it finds hides
the finding.

Definitions live in `.claude/agents/`. Edit the Markdown to change an agent's
behaviour; Claude Code notices within seconds, no restart needed.

## Three ways to call one

```
@agent-atlas-backend add a soft-delete flag to users
```

```
Use the atlas-qa subagent to review the migration in this diff
```

```
claude --agent atlas-architect      # a whole session as that agent
```

They also self-select: each `description` says when to delegate, so Claude will
reach for `atlas-ios` on a SwiftUI task without being told. To stop that for a
given agent, add `Agent(atlas-ios)` to `permissions.deny` in
`.claude/settings.json`.

---

## The five stages

| # | Stage | Lead | Command |
| --- | --- | --- | --- |
| 1 | Plan and research | `atlas-architect` | `/atlas-plan <task>` |
| 2 | Test plan | `atlas-qa` | folded into stage 1 |
| 3 | Implement | `atlas-backend` and/or `atlas-ios` | `/atlas-implement <task>` |
| 4 | Verify | `atlas-qa` | `/atlas-verify [area]` |
| 5 | Review | `atlas-qa` + `atlas-architect` in parallel | `/atlas-review [focus]` |
| 6 | Deliver | you | `/atlas-handoff`, then commit yourself |

Skills live in `.claude/skills/`. Stage 6 never commits on your behalf.

### What runs in parallel, and what must not

**Parallel is safe** when file sets are disjoint and neither side needs the
other's output:

- `atlas-backend` + `atlas-ios` on an unrelated backend change and UI change.
- `atlas-qa` + `atlas-architect` in stage 5 — both read-only on code.
- Any number of read-only investigations.

**Serial is required** when:

- The API contract changes. Backend regenerates `contracts/openapi/v1.json`
  first; iOS adapts after. In parallel, iOS codes against a contract being
  rewritten underneath it.
- Two agents would touch the same file. Split the task instead.
- Stage 4 — verification needs a tree nobody is editing.

For genuinely overlapping work, pass `isolation: "worktree"` on the `Agent`
call and the agent works on its own copy of the repo. Its changes then land on
a separate branch rather than in your working tree.

---

## Where decisions, risks and questions go

Conversations disappear. These files do not.

| Kind | Destination |
| --- | --- |
| Decision that is hard to reverse or sets a precedent | a new ADR in `docs/adr/`, from `template.md` |
| Risk that is real but accepted for now | Risks table in [`open-items.md`](open-items.md) |
| Question only you can answer | Open questions table, same file |
| Work deliberately left undone | Deferred table, same file |
| Fact a future session would rediscover the hard way | `CLAUDE.md` or a path-scoped rule in `.claude/rules/` |
| Source behind a decision | [`research-sources.md`](research-sources.md), with URL and date read |

ADRs are immutable once accepted: supersede, never edit. An ADR with no
downsides listed has not been thought through.

---

## A day in the workflow

**A feature across both sides.**

```
/atlas-plan let users deactivate their own account
```

Read the plan, approve or push back, then:

```
/atlas-implement the approved deactivation plan - backend first, it changes the contract
/atlas-verify all
/atlas-review the deactivation change
/atlas-handoff
```

**A backend-only fix.** Skip planning; it is reversible and sets no precedent.

```
@agent-atlas-backend the duplicate-email check races under concurrent inserts. Fix it, and add a test that fails without the fix.
/atlas-verify backend
```

**A question, not a change.**

```
Use the atlas-architect subagent to tell me whether the billing module should own its own schema or extend identity. Do not write code.
```

**Two independent changes at once.**

```
Run these in parallel: atlas-backend adds the readiness check for the outbox table, atlas-ios fixes the empty-state copy on the users list. The file sets are disjoint.
```

---

## Writing a good brief

A subagent starts with a **fresh, isolated context**. It cannot see this
conversation, your plan, or files already open. The brief is all it gets.

Include: the goal, the approach already agreed, the exact files it owns and the
paths it must not touch, the gate it must run, and what to hand back.

Then demand evidence. "All tests pass" tells you nothing on its own — ask for
the command and its output. Every agent here is instructed to paste it.

And keep sessions focused: `/clear` between unrelated tasks. Every turn re-sends
the whole conversation, so unrelated history costs money on every later turn.

---

## Limitations, and what to do instead

Honest list. None of these is invented capability.

**Path ownership is convention, not a sandbox.** Agent frontmatter can restrict
*tools* (`tools`, `disallowedTools`) but not *paths*. An agent with `Edit` can
edit anything.
*Closest alternatives:* the read-only reviewer pattern used for `atlas-qa`
(omit `Write`/`Edit` — genuinely enforced); `isolation: "worktree"` for hard
separation; or `permissions.deny` rules in `.claude/settings.json`, which apply
session-wide rather than per agent.

**`atlas-qa` can still write via Bash.** Removing `Write`/`Edit` stops the file
tools, not `sh`. Its prompt forbids mutating commands; that part is discipline.

**Subagents are not smarter by default.** Same model or smaller, on the same
task. They buy parallelism, context isolation and an independent read — not
extra intelligence. Pass `model: "opus"` on the `Agent` call for a genuinely
hard call.

**Path-scoped rules load on demand.** A rule in `.claude/rules/` with `paths:`
loads when a matching file is read or edited, not at session start. During stage
1 planning, before any file is opened, it is not in context. Repo-wide facts
therefore live in `CLAUDE.md`.
A malformed `paths:` block silently makes the rule global instead of erroring —
`claude --debug` shows parse errors.

**New agents need a restart — verified.** The documentation says
`.claude/agents/` is watched and a new definition is picked up within seconds.
In the session where these four were created, that did not happen: invoking
`atlas-qa` returned

```
Agent type 'atlas-qa' not found. Available agents: claude, claude-code-guide,
Explore, general-purpose, Plan, statusline-setup, web-fetch
```

so the agent registry was fixed at session start. Restart Claude Code (or open
a new session) before the first use. Expect the same for the skills in
`.claude/skills/`. Directories added with `--add-dir` are never watched.

**Caps.** 20 concurrent subagents per session; nesting three layers deep.

**Skills, not `.claude/commands/`.** Argument substitution is documented for
skills, and `.claude/commands/` is described as the older format. These skills
use only `$ARGUMENTS` (the whole argument string). If you write your own with
positional arguments, note that `$N` is **0-based**: `$0` is the first argument
and `$1` the second. `$ARGUMENTS[0]` is equivalent to `$0`.

**SwiftLint now runs; the simulator still does not.** Xcode 27.2 is installed,
so `swiftlint lint --strict` works and reports zero violations. Install it from
the official `portable_swiftlint.zip` — `brew install swiftlint` fails on macOS
27, which Homebrew treats as a pre-release Tier 2 configuration. The iOS
simulator has no runtimes installed (Xcode 27 downloads them separately), so
app-target and simulator tests remain unverified.

**CI cannot be used as evidence.** GitHub Actions is blocked by an account
billing lock, so no workflow has executed. Stage 6 relies on local gates until
that is cleared.

**Built-ins worth knowing**, so you do not reimplement them: `/code-review`
(multi-agent review of the diff), `/verify`, `/goal`, `/clear`, `/context`,
`/doctor`. The `atlas-*` prefix keeps these skills from shadowing them.
