---
paths:
  - "backend/**"
  - "services/**"
  - "contracts/**"
---

# Backend rules

Loaded on demand when a file under `backend/`, `services/` or `contracts/` is
read or edited. Applies whether or not the `atlas-backend` agent is driving.

## Gates

```
cd backend && dotnet build --configuration Release   # warnings are errors
cd backend && dotnet test                            # 35 tests; a skip is not a pass
cd backend && dotnet format --verify-no-changes
```

The Homebrew .NET formula is keg-only. `make` puts it on `PATH`; a bare
`dotnet` needs `export PATH="/opt/homebrew/opt/dotnet@10/bin:$PATH"`.

## Non-negotiable

- A module owns its Postgres schema. No query, join or foreign key crosses a
  schema boundary. Modules never reference each other.
- Never weaken `backend/tests/Atlas.Architecture.Tests` to make a change pass.
- A new module needs: explicit registration in `Program.cs`, an entry in
  `MigrationRunner.Migrations`, and its namespace prefix added to
  `ModuleNamespacePrefixes` in `ModuleBoundaryTests`.
- Package versions live in `backend/Directory.Packages.props` as
  `PackageVersion`. Never put `Version` on a `PackageReference` — central
  package management will refuse the build. Framework-aligned Microsoft
  packages stay on the 10.0.x line to match `net10.0`.
- Any endpoint or DTO change needs `make openapi` and the regenerated
  `contracts/openapi/v1.json` committed with it. CI fails on drift.

## Already-paid-for mistakes

- EF migrations are generated code, excluded from style analysis by the
  `[**/Migrations/*.cs]` section of `backend/.editorconfig`. Do not hand-edit
  them; do not re-enable analysis for them.
- `Atlas.DbMigrator` must use the **aspnet** base image, not `runtime`. It
  references `ServiceDefaults`, which carries a `FrameworkReference` on
  `Microsoft.AspNetCore.App`, and that propagates transitively. The `runtime`
  image exits 150 with "No frameworks were found".
- Every .NET entry point calls `AddServiceDefaults()`. Skipping it crashes at
  startup resolving `IClock`.
- `Guid.CompareTo` is not byte-order and disagrees with Postgres's `uuid`
  ordering. Keyset pagination relies on the comparison translating to SQL;
  `KeysetPaginationTranslationTests` guards it.
- Read the generated SQL (`make migration-sql MODULE=...`) before committing a
  migration. A changed `MaxLength` becomes a column narrowing.
