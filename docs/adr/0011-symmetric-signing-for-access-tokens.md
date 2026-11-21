# 0011. Symmetric signing for access tokens, for now

- **Status:** Accepted
- **Date:** 2026-11-21

## Context

This system now issues its own access tokens as well as validating them.
Signing can be symmetric (HS256: one secret, used to both sign and verify) or
asymmetric (RS256/ES256: a private key signs, a public key verifies).

The distinction only starts to matter when something other than the issuer
needs to verify a token. Today one service does both.

## Decision

HS256 with a single secret, supplied as configuration and never committed.
The Identity module signs; the API host validates; both read the same
`Identity:Auth` configuration section so the key, issuer and audience cannot
drift apart.

## Alternatives considered

| Option | Why not |
| --- | --- |
| RS256 now | The right answer eventually, but it means generating, storing, rotating and publishing a key pair, plus a JWKS endpoint, to solve a problem one service does not have. |
| Opaque access tokens checked against the database | Revocable, but a database read on every single request, which is exactly what a stateless access token avoids. |

## Consequences

**Good**

- One secret to manage. No key distribution, no JWKS endpoint.
- Validation is local and fast.

**Bad / accepted costs**

- **Any service that validates a token must hold the signing secret, which
  means it can also mint tokens.** That is acceptable with one service and
  unacceptable with several — it turns every consumer into a potential issuer.
- Rotating the secret invalidates every live access token at once. Refresh
  tokens survive, so clients recover within one refresh, but it is abrupt.

**What this makes harder later**

- The move to asymmetric signing is a real migration: issue with the new key
  while still accepting the old, publish a JWKS endpoint, then retire the
  secret. Doing it under time pressure would be unpleasant.

## Revisit when

A second service needs to validate tokens — that is the trigger, and it should
be treated as a blocker for that service rather than something to defer.
