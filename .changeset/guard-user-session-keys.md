---
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
---

Validate the session key and clamp the diagnostic fields in `ISessionManager`

`TrackSessionAsync` rejects a blank session key, or one whose URL-encoded form exceeds the 231 bytes the
distributed cache leaves for a key under `CacheId.AuthTicket`, and does so synchronously at the call site
before any I/O. It also clamps `userAgent` and `ipAddress` to their mapped column lengths, so an over-length
client `User-Agent` header can no longer fail the insert and leave a cached ticket with no tracking row.

`RevokeSessionAsync` and `UntrackSessionAsync` report such a key as "no session" rather than passing it to the
ticket cache, which throws on an over-long key and silently redirects a blank one to the shared
`CacheId.AuthTicket` entry. `RevokeOtherUserSessionsAsync` now rejects a `keepSessionKey` that no tracked session could carry — a
whitespace-only one, which previously passed its non-empty check, and one over the cache-key budget, which
`TrackSessionAsync` refuses to store. Either degraded the filter to "revoke every session", signing out the
very session the caller asked to keep.
