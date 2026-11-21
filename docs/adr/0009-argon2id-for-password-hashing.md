# 0009. Argon2id for password hashing

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

Storing passwords means assuming the database will one day be copied. The
only question is how much an attacker gains when it is. A fast hash gives them
most of the passwords within days; a memory-hard one makes large-scale
cracking expensive enough to be uneconomic.

The repository otherwise avoids third-party dependencies. Password hashing is
where that preference meets a stronger one.

## Decision

Argon2id, via `Konscious.Security.Cryptography.Argon2`, at the OWASP minimum
of 19 MiB memory, two iterations, one degree of parallelism.

Hashes are stored in PHC string format — `$argon2id$v=19$m=…,t=…,p=…$salt$hash` —
so each hash carries the parameters it was produced with. The cost can be
raised later without invalidating existing hashes.

## Alternatives considered

| Option | Why not |
| --- | --- |
| PBKDF2 (first-party, `Microsoft.AspNetCore.Cryptography.KeyDerivation`) | Acceptable to OWASP at 600k iterations and adds no dependency, but it is not memory hard, so GPU cracking stays cheap. The dependency saving is not worth the weaker guarantee on the one value we most need to protect. |
| bcrypt | Also third-party, and caps passwords at 72 bytes. No advantage over Argon2id. |
| ASP.NET Core Identity wholesale | Brings users, roles, stores, UI scaffolding and its own schema. We would fight its shape far more than we would gain. |

## Consequences

**Good**

- The strongest widely-recommended option, at parameters we can raise over time.
- A leaked table is a slow, expensive problem for an attacker rather than a fast one.
- Self-describing hashes make the cost upgrade a non-event.

**Bad / accepted costs**

- A third-party package in the most security-critical path in the system. It
  must be watched: a compromised release here is catastrophic rather than
  inconvenient. Dependabot covers it and the lock file pins it.
- Each verification costs 19 MiB and milliseconds. That is the point, but it
  makes sign-in a measurably heavier endpoint, and it means the login path is
  a denial-of-service target — hence the rate limiter and lockout in front of it.
- Tests that exercise real hashing are slow by nature, so they are kept few
  and deliberate.

**What this makes harder later**

- Changing algorithm means rehashing on next sign-in, which the PHC format
  supports but which nothing currently implements.

## Revisit when

OWASP changes its recommendation, the package stops being maintained, or
sign-in latency becomes a measured problem rather than a theoretical one.
