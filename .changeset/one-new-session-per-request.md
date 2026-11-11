---
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
---

Refuse a second new session on one request, and document the calls for a password reset that signs in the person

A second call to `HttpContext.SignInNewSessionAsync` on the same request throws `InvalidOperationException`. Before, the second call could not end the session of the first call, and that session stayed stored until it expired. `ISessionLifecycle.CredentialsChangedAsync` can call the method itself, so do not sign in again on a request that calls it.

A password reset endpoint that signs in the person on the same request calls `ISessionLifecycle.AccessRevokedAsync(accountId)`, then `SignInNewSessionAsync`. Revocation participants get `SessionRevocationReason.AccessRevoked` for such a reset.
