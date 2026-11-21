# API conventions

Base URL: `/api/v1`. Full contract:
[`contracts/openapi/v1.json`](../../contracts/openapi/v1.json). Interactive
reference at `/scalar` outside production.

## Versioning

URL-segment versioning (`/api/v1/...`) via `Asp.Versioning`. Clients already
installed on phones cannot be forced to upgrade, so the API must be able to
serve two shapes at once.

Add a version when a change is breaking — a removed or renamed field, a
narrowed type, a changed status code, a new required parameter. Adding an
optional field or a new endpoint is not breaking and does not need one.

```csharp
var v2 = app.NewVersionedApi("Atlas").MapGroup("/api/v{version:apiVersion}").HasApiVersion(2, 0);
```

## Errors

Every error response is RFC 9457 `application/problem+json`, from the .NET API
and from the Node and Python services alike:

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "That email address is already registered.",
  "instance": "POST /api/v1/users",
  "code": "identity.user.email_taken",
  "traceId": "0af7651916cd43dd8448eb211c80319c"
}
```

- **`code`** is part of the contract. Stable, dotted, `module.entity.problem`.
  Clients branch on this, never on `detail`.
- **`detail`** is for humans and may change at any time.
- **`traceId`** is the distributed-trace id. Surface it in client error UI —
  it turns a support conversation into a lookup.

Validation failures (400) add a per-field `errors` object.

| Status | When |
| --- | --- |
| 400 | Request shape is invalid |
| 401 | Missing or invalid token |
| 403 | Authenticated but not allowed |
| 404 | Does not exist, or the caller may not know it does |
| 409 | Valid request, but state does not permit it |
| 429 | Rate limited. Honour `Retry-After` |
| 5xx | Our fault. The `detail` is deliberately generic |

## Pagination

Keyset ("seek") pagination, not offset:

```http
GET /api/v1/users?limit=50
GET /api/v1/users?limit=50&cursor=01924f8e-...
```

```json
{ "items": [ ... ], "nextCursor": "01924f8e-..." }
```

`nextCursor` is `null` on the last page — that, not an empty `items` array, is
the end-of-collection signal.

Offset pagination gets slower the further you go and skips or repeats rows
when the underlying data changes between pages. Keyset paging costs the same
at page 10,000 as at page 1 and cannot repeat a row. `limit` is clamped to 200.

The cursor is an opaque string. Do not parse it; today it is the UUIDv7
primary key, and that is an implementation detail.

## Authentication

JWT bearer tokens. Secure by default: a fallback authorization policy requires
an authenticated user, so a new endpoint is protected unless it explicitly
calls `.AllowAnonymous()`. The API refuses to start with `Auth:Enabled=false`
outside `Development`.

## Health

| Endpoint | Purpose |
| --- | --- |
| `/health/live` | Process liveness. Never touches a dependency. A failure here restarts the pod. |
| `/health/ready` | Readiness, including dependencies. A failure removes the pod from the load balancer. |

Do not point a load balancer at `/health/live` or an orchestrator's liveness
probe at `/health/ready` — that inversion turns a brief database blip into a
fleet-wide restart.

## Conventions for new endpoints

- camelCase JSON (the ASP.NET Core default); the Swift models match field
  names directly.
- Timestamps are ISO 8601 with an explicit offset, UTC. Note that
  System.Text.Json emits up to seven fractional digits, which Foundation's
  built-in `.iso8601` decoder rejects — `AtlasJSON` in `AtlasNetworking`
  handles both shapes.
- `POST` returns `201` with a `Location` header.
- Return a domain `Result<T>` and map it with `ToHttpResult`, so status codes
  come from the domain rather than from each handler's guesswork.
- Validate the request shape with a FluentValidation validator and
  `.WithRequestValidation<T>()`. Domain invariants still belong in the domain —
  the API is only one possible caller.
- Regenerate the contract: `make openapi`.
