# Atlas

iOS app (SwiftUI) plus a C#/.NET 10 modular-monolith backend, with Node and
Python satellite services. Architecture, the five boundary rules and the
reasoning behind them are in `docs/architecture.md` and `docs/adr/` — read those
rather than inferring the shape from the tree.

The agentic workflow, including which agent owns what, is in
`docs/workflow/README.md`.

## Commands

```
make doctor         # which toolchains are present, and what each missing one blocks
make up / down      # local stack (Postgres, OTel collector, API, workers)
make deps           # only Postgres + collector + Jaeger, to run the app from an IDE
make test           # every suite: backend 35, iOS 38, node 9, python 13
make lint           # check only, changes nothing
make format         # fix what can be fixed, all four languages
make openapi        # regenerate the committed API contract
make migrate        # apply pending migrations locally
make ios-project    # regenerate ios/Atlas.xcodeproj from project.yml
```

Per-area commands and gates: `make test-backend`, `make test-ios`,
`make test-services`. Run `make` alone to list every target.

The Homebrew .NET formula is keg-only. `make` puts it on `PATH`; a bare
`dotnet` in a shell needs
`export PATH="/opt/homebrew/opt/dotnet@10/bin:$PATH"`.

## Warnings are errors

In C# (`TreatWarningsAsErrors`), in Swift (`-warnings-as-errors`), in the Xcode
app target, and via `--strict` linting in the services. A change that builds
with warnings does not build.

## A skip is not a pass

The 10 backend integration tests skip when no container runtime is present,
printing "No container runtime detected". That is unverified coverage, not
green. To run them: `brew install colima docker && colima start`.

SwiftLint needs `sourcekitdInProc.framework`, which ships only with a full
Xcode install; `make lint` skips it and says so when it is absent. Xcode 27.2
is installed here, so it runs. `brew install swiftlint` does **not** work on
macOS 27 (Homebrew pre-release Tier 2: `unknown or unsupported macOS version`);
use the official `portable_swiftlint.zip` release.

The iOS simulator is still unavailable: Xcode 27 downloads runtimes separately
and none is installed (Xcode > Settings > Components).

GitHub Actions is currently blocked by an account billing lock, so no workflow
has ever executed. Do not cite CI as evidence.

## Who owns which paths

One owner per path. Two agents editing the same file is the failure this
split exists to prevent.

| Paths | Owner |
| --- | --- |
| `backend/**`, `services/**`, `contracts/**` | `atlas-backend` |
| `ios/**` | `atlas-ios` |
| `docs/adr/**`, `docs/architecture.md` | `atlas-architect` |
| `docs/workflow/**` | whoever runs `/atlas-handoff` |

`atlas-qa` owns no code: it has no `Write` or `Edit` tool and reports findings
for the owning agent to apply.

`contracts/openapi/v1.json` is generated. The backend regenerates it with
`make openapi`; iOS consumes it and never edits it. Hand-editing it fails CI.

## Where decisions and risks go

- A decision that is hard to reverse or sets a precedent: a new ADR in
  `docs/adr/`, from `template.md`. ADRs are immutable once accepted —
  supersede, never edit.
- Risks, open questions and deliberately deferred work:
  `docs/workflow/open-items.md`.
- A source consulted for a decision: `docs/workflow/research-sources.md`, with
  the URL and the date it was read.

## Repo etiquette

- Commit or push only when the user asks. The repo's default branch is `main`
  and its remote is GitHub.
- Any endpoint or DTO change needs `make openapi` and the regenerated contract
  in the same change.
- Never weaken `backend/tests/Atlas.Architecture.Tests` to make a change pass.
  If a boundary rule is wrong, change it in an ADR and in the test.
- Migrations must be backward compatible with the deployed version: both
  versions serve traffic during a rolling deploy. See
  `docs/runbooks/database-migrations.md`.
