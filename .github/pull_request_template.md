## What and why

<!-- What changes, and what problem it solves. Link the issue. -->

## How to verify

<!-- The steps a reviewer can actually run. "Tested locally" is not a step. -->

## Checklist

- [ ] Tests cover the change (and fail without it)
- [ ] `make test` passes locally
- [ ] Public API changes are reflected in `contracts/openapi/` (run `make openapi`)
- [ ] Database changes ship as a migration, and are backward compatible with the
      currently deployed version (both versions serve traffic during a rolling deploy)
- [ ] No secrets, tokens or personal data in code, tests, fixtures or logs
- [ ] An architectural decision is recorded in `docs/adr/` if this sets a precedent

## Rollout and rollback

<!-- Anything beyond "merge and deploy": feature flags, migration ordering,
     config that must exist first, how to roll this back. -->
