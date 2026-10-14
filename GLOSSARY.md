# CSAG Blueprint packages

The shared building blocks of CSAG Blueprint applications. This glossary names the concepts the packages own, so code, docs and reviews use one word for each.

## Sessions

**Session**:
One signed-in presence of a user account, held server-side and identified to the browser only by the session cookie. It belongs to one account, is scoped to at most one tenant at a time, and ends when it expires, is signed out, or is revoked.
_Avoid_: Login, ticket, cookie

**Session lifecycle**:
Everything that happens to sessions after sign-in in response to what happens to accounts and tenants — which sessions end, which keep running with rebuilt claims. The packages own it; the application only states intents.
_Avoid_: Session management

**Tracking record**:
The stored description of a session — its account, tenant, expiry, and the device and address it signed in from — that lets sessions be listed and revoked.
_Avoid_: Session row, active session entry

**Revocation**:
Ending sessions before they expire, so their next request is unauthenticated.
_Avoid_: Invalidation, logout (logout is the user ending their own current session)

**Revocation participant**:
An application component that ends the application's own credentials (refresh tokens, API keys) whenever the session lifecycle revokes sessions, for a subject that is a user or a service account.
_Avoid_: Revocation hook, listener

**Session eligibility**:
The application's answer to whether an account may hold a session at all; asked at sign-in and whenever a session slides.
_Avoid_: Account status check

## Session lifecycle intents

**Credentials changed**: an account's password or second factor changed; its sessions end, except the caller's own session when it belongs to that account.

**Access revoked**: an account lost access, everywhere or in one tenant; its sessions there end.

**Claims changed**: something an account's session claims are built from changed; its sessions keep running with rebuilt claims.

**Tenant removed**: a tenant was removed; every session scoped to it ends.

**Switch my session's tenant**: the caller moves their own session to another tenant they are a member of.

**List my sessions**: the caller sees their account's sessions, with their own marked as current.

**Revoke one of my sessions**: the caller ends one of their account's sessions; ending their own signs them out.
