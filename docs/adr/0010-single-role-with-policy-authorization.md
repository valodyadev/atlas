# 0010. One role per user, authorised through policies

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

The brief asked for an authorization model that can grow into roles and
permissions without a rewrite. The temptation is to build the full
users-roles-permissions triangle immediately. The cost of that is real —
three tables, joins on every request, a caching question — and on day one
there are exactly two kinds of user.

The thing that actually makes authorization hard to change later is not the
data model. It is endpoints that ask `if (user.Role == "admin")`. Those have
to be found and rewritten one by one.

## Decision

A single `role` column on the user, carried as a claim in the access token.

Endpoints authorise against **named policies**, never against a role string
directly. The policy is the seam: today it reads one claim, tomorrow it can
read a permission set loaded from a join table, and no endpoint changes.

## Alternatives considered

| Option | Why not |
| --- | --- |
| Roles and permissions tables now | Pays the full cost before there is a second role. The hard part to change is the call sites, and this does not help with those. |
| Role checks inline at each endpoint | Exactly the thing that makes the model expensive to evolve. |
| An external authorization service | Enormous for a system with two roles and no users. |

## Consequences

**Good**

- One column, one claim, no joins on the request path.
- The growth path is additive: add the tables, change what populates the
  policy, leave every endpoint alone.

**Bad / accepted costs**

- One role per user. Someone who needs two roles needs the schema change
  sooner than planned.
- The role lives in the token, so a role change does not take effect until the
  access token expires — up to fifteen minutes. The security stamp exists to
  force that sooner when it matters, but nothing currently calls it on a role
  change.

**What this makes harder later**

- Nothing structurally, provided the policy discipline holds. If a `Role ==`
  comparison appears in an endpoint, this ADR has been broken.

## Revisit when

A second role needs to coexist on one user, or a permission needs granting
independently of a role.
