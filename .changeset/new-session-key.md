---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Start a session under a new key at every sign-in and credential change, so a copied session cookie stops working

`HttpContext.SignInNewSessionAsync(principal)` (Csag.Blueprint.Infrastructure) signs a session in under a new key. A session the request still holds ends first. A plain `HttpContext.SignInAsync` on a request that still carries a live session cookie keeps that cookie's key (dotnet/aspnetcore#22135), so a copy of the old cookie reaches the new session. Use the new method for every sign-in to the Identity application cookie.

**Behaviour change:** `ISessionLifecycle.CredentialsChangedAsync` ends every session of the account, the caller's included. When the caller's session belongs to the account, the caller continues in a new session with the same claims and tenant, under a new key and with a fresh lifetime, and the response sets its cookie. Before, the caller kept its session and its key, so a copied cookie stayed valid after a password change. A client that stores the session cookie itself must take the new one from the response, as browsers do. When `ISessionEligibility` refuses the account, or a step of the intent fails, the caller is signed out instead.
