# Configuration and secrets

## The rule

**No secret is ever committed, in any form, in any branch.** Git history is
permanent and effectively public: once a credential is pushed it must be
rotated, not deleted. Removing it in a later commit changes nothing.

Enforced in three places:

1. `.gitignore` excludes `.env`, `*.pem`, `*.p12`, `appsettings.*.Local.json`
   and friends.
2. The pre-commit hook (`make install-hooks`) blocks those paths and scans the
   staged diff for the obvious credential shapes.
3. `gitleaks` runs in CI over the **full history**, on every push and weekly.

## Configuration precedence

Later sources win:

```
appsettings.json              committed, no secrets, safe defaults
appsettings.{Environment}.json committed, no secrets
user secrets                  local only, Development only, outside the repo
environment variables         how deployments supply everything
command line                  overrides, for debugging
```

ASP.NET Core maps nested keys with a double underscore, so
`ConnectionStrings:Default` is `ConnectionStrings__Default` in the environment.

## Where secrets actually live

| Environment | Mechanism |
| --- | --- |
| Local | `dotnet user-secrets`, or `.env` (git-ignored) for the compose stack |
| CI | GitHub Actions secrets, scoped to an Environment with required reviewers |
| Deployed | Your cloud secret manager, projected into the pod as a Kubernetes Secret by an operator (External Secrets, the AWS/Azure CSI driver, Vault Agent) |

The Kustomize manifests reference `secretRef: atlas-api-secrets` and never
define it. That Secret is created by the operator, so no credential exists in
this repository or in any rendered manifest.

### Local secrets, without a `.env`

```bash
cd backend/src/Atlas.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=atlas;Username=atlas;Password=..."
```

Stored outside the repository, per user, per project. Unlike `.env` it cannot
be committed by accident.

### Prefer federated identity to secrets

Anywhere one service authenticates to another, use a workload identity rather
than a stored credential:

- GitHub Actions → cloud: OIDC federation, not a long-lived access key.
- Pod → database: IAM database authentication where the provider supports it.
- Pod → cloud API: the workload identity attached to the service account.

A credential that does not exist cannot leak, cannot expire unnoticed, and
cannot be committed.

## Rotation

Assume every secret will need rotating at an inconvenient moment, and keep
that cheap:

- Nothing reads a secret at module load or caches it for the process lifetime
  beyond what the configuration system does; a restart picks up a new value.
- Connection strings and tokens are never logged. The .NET `ProblemDetails`
  customisation never includes configuration, Fastify's logger redacts
  `authorization` and `cookie`, and the API returns a generic message for any
  5xx so an internal exception cannot carry a credential to a client.
- The iOS app stores its token in the Keychain with
  `ThisDeviceOnlyAfterFirstUnlock`, so it is not carried into an unencrypted
  backup or a restored device.

## If a secret is committed

In this order:

1. **Rotate it.** Immediately, before anything else. Treat it as compromised
   the moment it was pushed.
2. Revoke the old value and check the provider's audit log for use.
3. Remove it from history (`git filter-repo`), force-push, and tell everyone
   with a clone to re-clone. This is cleanup, not remediation — step 1 is the
   remediation.
4. Add a pattern for its shape to the pre-commit hook so the same class of
   mistake is caught next time.

## What is safe to commit

- API base URLs, including staging ones (`ios/App/Config/*.xcconfig`).
- Non-secret feature flags and tuning values.
- `.env.example`, with placeholder values only.
- Local-development passwords that exist only in `compose.yaml` defaults
  (`local-dev-only`) and are unreachable from anywhere else.
