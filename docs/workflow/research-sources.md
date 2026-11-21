# Research sources

Sources behind the workflow in this directory. Each entry records what it was
used for and the date it was read, because documentation moves.

Add an entry whenever a decision rests on something outside the repo. Do not
assert a version number, release date or deprecation from memory.

## Claude Code configuration and practice

All read **2026-11-21**. None of these pages displayed a publication or
last-updated date; the version markers noted are those visible in the page text.

| Source | Used for |
| --- | --- |
| [Subagents](https://code.claude.com/docs/en/sub-agents) | The `.claude/agents/*.md` format: that only `name` and `description` are required; multi-word fields are camelCase (`disallowedTools`, `maxTurns`, `permissionMode`); `tools` is a comma-separated string or YAML list and inherits everything if omitted; `model` accepts `sonnet`/`opus`/`haiku`/`fable`, a full id, or `inherit`; the `color` set; that unrecognised fields are ignored without error; that `description` drives automatic delegation and "use proactively" encourages it; that each subagent gets a fresh isolated context; the 20-concurrent and 3-deep caps; and that agent files are watched and picked up within seconds. Page text referenced versions around v2.1.63–v2.1.296. |
| [Skills](https://code.claude.com/docs/en/skills) | The `.claude/skills/<name>/SKILL.md` format and argument substitution: `$ARGUMENTS` for the whole string, and that `$N` is **0-based** shorthand for `$ARGUMENTS[N]` — so `$0` is the first argument. This is why these skills use only `$ARGUMENTS`. |
| [Slash commands](https://code.claude.com/docs/en/slash-commands) | That custom commands have been merged into skills, that `.claude/commands/` is "the older format and still works", and that skills are recommended for new work — the reason this setup uses skills. Also the frontmatter field list and `:`-namespacing of subdirectories. Page text referenced v2.1.246–v2.1.309. |
| [Memory](https://code.claude.com/docs/en/memory) | `CLAUDE.md` locations and precedence; that `.claude/rules/*.md` is real and that **`paths` is the only field a rule file reads**, as a YAML list of globs relative to the directory containing `.claude`; that a path-scoped rule loads on demand when a matching file is read or edited, not at session start; that malformed frontmatter makes the rule load globally instead of erroring; and the guidance to keep `CLAUDE.md` under about 200 lines. Page text referenced v2.1.234–v2.1.309. |
| [Best practices](https://code.claude.com/docs/en/best-practices) | What belongs in `CLAUDE.md` (exact commands, misleading code, decisions not in the repo, anything corrected twice) and what does not (anything readable from the code); "give Claude something it can run that comes back pass or fail"; demanding evidence because "all tests pass" on its own "tells you very little"; that subagents are not smarter by default; the read-only reviewer pattern via restricted `tools`; "skip style" in review prompts; `/clear` between unrelated tasks; and that emphasis such as "IMPORTANT" in `CLAUDE.md` is outdated advice. Note: `anthropic.com/engineering/claude-code-best-practices` now returns a 308 redirect to this page, so there is no separate engineering-post text. |

Verification caveat: these pages were read by delegated fetch agents. The
subagent and best-practices pages are long and were read verbatim only in part
(roughly the first 80,000 and 40,000 characters respectively); the remainder was
summarised. Every field name used in this repo's configuration comes from the
verbatim portion, and the `paths` and argument-placeholder syntax was
cross-checked a second time before use.

## Facts about this project's dependencies

| Source | Date read | Used for |
| --- | --- | --- |
| [docker-library/postgres#1259](https://github.com/docker-library/postgres/pull/1259) | 2026-11-21 | Postgres 18 keeps its cluster in a major-version subdirectory under a single mount, so the volume must be mounted at `/var/lib/postgresql`, not `/var/lib/postgresql/data`. Cited in `infra/docker/compose.yaml`. |
| [actions/runner-images macos-15 image manifest](https://raw.githubusercontent.com/actions/runner-images/main/images/macos/macos-15-Readme.md) | 2026-11-21 | Confirmed `Xcode_16.4` exists on the `macos-15` runner, so the pin in `.github/workflows/ios.yml` is valid. |
| NuGet package manifests for `Microsoft.Extensions.Hosting` 11.0.0 and `Microsoft.Extensions.Http.Resilience` 11.0.1 | 2026-11-21 | Both ship a `net10.0` asset, so those major bumps are compatible with the pinned `net10.0` target. Verified by building against them rather than by reading alone. |
