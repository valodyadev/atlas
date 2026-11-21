---
name: atlas-verify
description: Stage 4 of the Atlas workflow. Run the quality gates for a change and report pass, fail or unverified for each with the real command output as evidence.
argument-hint: [backend | ios | services | all - default all]
---

# Verify: $ARGUMENTS

Scope defaults to everything if no area is given.

Use the **atlas-qa** agent. It has no `Write` or `Edit` tool, so it cannot
quietly fix what it finds.

## What it must run

```
make test-backend     # 35 tests; the 10 integration tests need a container runtime
make test-ios         # 38 tests
make test-services    # vitest 9 + pytest 13
make lint             # dotnet format, swiftformat, eslint, tsc, prettier, ruff, mypy
cd backend && dotnet build --configuration Release
make openapi && git --no-pager diff --stat -- contracts/openapi
```

## Three outcomes, never two

Every gate is **pass**, **fail** or **unverified**. Folding "unverified" into
"pass" is the failure mode this stage exists to prevent.

Known unverifiable-locally cases, which must be reported as such:

- The backend integration tests **skip** without a container runtime. A skip is
  not a pass. With Colima: `brew install colima docker && colima start`.
- **SwiftLint** runs here (Xcode 27.2). `make lint` skips it only when Xcode is
  absent. `brew install swiftlint` fails on macOS 27; use the portable release.
- **The iOS simulator has no runtimes installed**, so app-target and simulator
  tests are unverified.
- **GitHub Actions is blocked by an account billing lock**, so CI has never run
  and cannot be cited as evidence.

## Report

A table of gate, outcome, command, and the decisive output line — then a single
verdict. If anything is unverified, the verdict says so explicitly rather than
reading as green.
